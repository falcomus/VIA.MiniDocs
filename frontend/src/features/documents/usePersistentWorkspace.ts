import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError, errorMessage } from '../auth/api';
import type { ApiClient } from '../auth/api';
import type { DocumentSummary } from './document.types';
import type { Topic } from '../topics/topic.types';
import type { TagDefinition } from './tags';
import type { components, paths } from '../../../../backend/contracts/fixture.generated';

type TopicItem = components['schemas']['TopicItem'];
type TagItem = components['schemas']['TagItem'];
type DocumentItem = components['schemas']['DocumentItem'];
interface Page<T> {
  items: T[];
  nextCursor: string | null;
}
type Move =
  paths['/api/v1/documents/batch-move']['post']['requestBody']['content']['application/json']['items'][number];
interface Snapshot {
  topics: TopicItem[];
  tags: TagItem[];
  documents: DocumentItem[];
}

function summary({ document, etag }: DocumentItem): DocumentSummary {
  return {
    id: document.id,
    etag,
    title: document.title,
    fileName: document.fileName ?? undefined,
    kind: document.kind === 'meeting' ? 'Besprechung' : 'Notiz',
    date: document.documentDate,
    topicId: document.topicId,
    tagIds: document.tagIds,
    pages: document.pageCount === null ? null : Number(document.pageCount),
    metadataOnly: true,
  };
}

export function usePersistentWorkspace(
  api: ApiClient,
  workspaceId: string,
  recheckSession: () => void,
  revalidation = 0,
) {
  const [snapshot, setSnapshot] = useState<Snapshot | null>(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [undo, setUndo] = useState<Move[] | null>(null);
  const current = useRef<AbortController | null>(null);
  const inFlight = useRef(false);
  const notification = useRef<BroadcastChannel | null>(null);
  const needsReload = useRef(false);
  const revision = useRef(0);
  const lastRevalidation = useRef(revalidation);
  // A failed or superseded read must not authorize further writes.
  const writable = useRef(false);
  const report = useCallback(
    (failure: unknown, signal: AbortSignal) => {
      if (signal.aborted) return;
      if (failure instanceof ApiError && (failure.status === 401 || failure.code === 'forbidden'))
        recheckSession();
      else if (failure instanceof ApiError && failure.status === 412)
        setError(
          'Der Eintrag wurde zwischenzeitlich geändert. Bitte öffne die Bearbeitung erneut.',
        );
      else if (failure instanceof ApiError && failure.status === 404)
        setError('Der Eintrag ist nicht mehr verfügbar. Bitte prüfe die aktualisierte Liste.');
      else if (failure instanceof ApiError && failure.code === 'topic_name_conflict')
        setError('Ein Thema mit diesem Namen existiert bereits auf dieser Ebene.');
      else if (failure instanceof ApiError && failure.code === 'topic_name_reserved')
        setError('Dieser Themenname ist auf der Hauptebene reserviert.');
      else setError(errorMessage(failure));
    },
    [recheckSession],
  );
  const load = useCallback(
    async (signal: AbortSignal, clearError = true) => {
      const read = ++revision.current;
      writable.current = false;
      try {
        const data = await readSnapshot(api, signal);
        if (signal.aborted || read !== revision.current) return false;
        writable.current = true;
        setSnapshot(data);
        if (clearError) setError('');
        return true;
      } catch (failure) {
        if (read === revision.current) report(failure, signal);
        return false;
      }
    },
    [api, report],
  );

  useEffect(() => {
    const scope = new AbortController();
    current.current = scope;
    writable.current = false;
    const channel =
      typeof BroadcastChannel === 'undefined'
        ? null
        : new BroadcastChannel('minidocs-metadata-v1:' + workspaceId);
    notification.current = channel;
    function refresh() {
      if (inFlight.current) needsReload.current = true;
      else void load(scope.signal);
    }
    if (channel) channel.onmessage = refresh;
    refresh();
    return () => {
      scope.abort();
      current.current = null;
      writable.current = false;
      channel?.close();
    };
  }, [workspaceId, load]);

  useEffect(() => {
    if (lastRevalidation.current === revalidation) return;
    lastRevalidation.current = revalidation;
    const signal = current.current?.signal;
    if (!signal) return;
    if (inFlight.current) needsReload.current = true;
    else void load(signal);
  }, [load, revalidation]);

  async function run<T>(mutate: (signal: AbortSignal) => Promise<T>): Promise<T | null> {
    const signal = current.current?.signal;
    if (!signal || inFlight.current) return null;
    if (!writable.current) {
      setError('Die Daten sind nicht aktuell. Bitte zuerst neu laden.');
      return null;
    }
    inFlight.current = true;
    revision.current++;
    setBusy(true);
    setError('');
    try {
      const result = await mutate(signal);
      if (signal.aborted) return null;
      notification.current?.postMessage('changed');
      if (!(await load(signal)) && !signal.aborted)
        setError('Änderung gespeichert. Neuladen fehlgeschlagen. Bitte zuerst neu laden.');
      return signal.aborted ? null : result;
    } catch (failure) {
      if (signal.aborted) return null;
      report(failure, signal);
      // A transport failure may hide a committed write. Refresh, never retry it automatically.
      await load(signal, false);
      return null;
    } finally {
      while (!signal.aborted && needsReload.current) {
        needsReload.current = false;
        await load(signal, false);
      }
      inFlight.current = false;
      if (!signal.aborted) setBusy(false);
    }
  }

  function etag(items: readonly DocumentItem[], id: string) {
    return items.find((row) => row.document.id === id)?.etag;
  }
  function applyMoves(items: Move[]) {
    return run(async (signal) => {
      if (items.some((item) => !item.etag)) throw new ApiError(428, 'version_required');
      const { data } = await api.request<{ items: (DocumentItem & { previousTopicId: string })[] }>(
        '/api/v1/documents/batch-move',
        'POST',
        { items },
        undefined,
        signal,
      );
      return data.items;
    });
  }

  async function assignTag(
    items: { id: string; etag?: string }[],
    tagId: string,
    on: boolean,
    signal: AbortSignal,
  ) {
    await api.request(
      '/api/v1/documents/batch-tags',
      'POST',
      { items, tagId, on },
      undefined,
      signal,
    );
  }

  return {
    ready: snapshot !== null,
    error,
    busy,
    topics:
      snapshot?.topics.map(({ topic, etag }): Topic => ({
        etag,
        id: topic.id,
        name: topic.name,
        parentId: topic.parentId,
        ...(topic.isSystem ? { systemKey: 'UNASSIGNED' } : {}),
      })) ?? [],
    tags: snapshot?.tags.map(({ tag, etag }) => ({ ...tag, etag })) ?? [],
    documents: snapshot?.documents.map(summary) ?? [],
    undo: undo ? new Map(undo.map(({ id, targetTopicId }) => [id, targetTopicId])) : null,
    reload: async () => {
      const signal = current.current?.signal;
      if (!signal || inFlight.current) return;
      await load(signal);
    },
    saveTopic: (
      id: string | null,
      fields: { name?: string; parentId?: string | null },
      expectedEtag?: string,
    ) =>
      run(async (signal) => {
        if (id && !expectedEtag) throw new ApiError(428, 'version_required');
        return (
          await api.request<TopicItem['topic']>(
            '/api/v1/topics' + (id ? '/' + id : ''),
            id ? 'PATCH' : 'POST',
            fields,
            id ? { 'If-Match': expectedEtag ?? '' } : undefined,
            signal,
          )
        ).data;
      }),
    deleteTopic: (id: string, expectedEtag?: string) =>
      run(async (signal) => {
        if (!expectedEtag) throw new ApiError(428, 'version_required');
        await api.request(
          '/api/v1/topics/' + id,
          'DELETE',
          undefined,
          { 'If-Match': expectedEtag ?? '' },
          signal,
        );
        return true;
      }),
    saveTag: async (
      tag: TagDefinition,
      mode: 'create' | 'update',
      selected?: readonly DocumentSummary[],
    ) => {
      const result = await run(async (signal) => {
        const updating = mode === 'update';
        if (updating && !tag.etag) throw new ApiError(428, 'version_required');
        const { name, description, color } = tag;
        const items = selected?.map(({ id, etag }) => ({ id, etag }));
        if (items?.some((item) => !item.etag)) throw new ApiError(428, 'version_required');
        const { data } = await api.request<components['schemas']['TagDto']>(
          '/api/v1/tags' + (updating ? '/' + tag.id : ''),
          updating ? 'PATCH' : 'POST',
          { name, description, color },
          updating ? { 'If-Match': tag.etag! } : undefined,
          signal,
        );
        if (items?.length && !signal.aborted) {
          try {
            await assignTag(items, data.id, true, signal);
          } catch (failure) {
            return { assignment: failure };
          }
        }
        return {};
      });
      if (!result) return null;
      if ('assignment' in result) {
        const signal = current.current?.signal;
        if (!signal || signal.aborted) return null;
        report(result.assignment, signal);
        return 'assignment_unconfirmed' as const;
      }
      return true;
    },
    deleteTag: (id: string, expectedEtag?: string) =>
      run(async (signal) => {
        if (!expectedEtag) throw new ApiError(428, 'version_required');
        await api.request(
          '/api/v1/tags/' + id,
          'DELETE',
          undefined,
          { 'If-Match': expectedEtag ?? '' },
          signal,
        );
        return true;
      }),
    setTag: (ids: readonly string[], tagId: string, on: boolean) =>
      run(async (signal) => {
        const items = ids.map((id) => ({ id, etag: etag(snapshot?.documents ?? [], id) }));
        if (items.some((item) => !item.etag)) throw new ApiError(428, 'version_required');
        await assignTag(items, tagId, on, signal);
        return true;
      }),
    move: async (
      ids: readonly string[],
      targetTopicId: string,
      versions?: ReadonlyMap<string, string>,
    ) => {
      if (!snapshot || !ids.length) return false;
      const items = ids.map((id) => ({
        id,
        targetTopicId,
        etag: (versions ? versions.get(id) : etag(snapshot.documents, id)) ?? '',
      }));
      const changed = await applyMoves(items);
      if (!changed) return false;
      const reverse = changed
        .filter((row) => row.previousTopicId !== row.document.topicId)
        .map((row) => ({
          id: row.document.id,
          targetTopicId: row.previousTopicId,
          etag: row.etag,
        }));
      setUndo(reverse.length ? reverse : null);
      return true;
    },
    undoMove: async () => {
      if (!undo?.length || !snapshot) return;
      const topics = new Set(snapshot.topics.map(({ topic }) => topic.id));
      const inbox = snapshot.topics.find(({ topic }) => topic.isSystem)?.topic.id;
      const items = undo.map((entry) => ({
        ...entry,
        targetTopicId: topics.has(entry.targetTopicId)
          ? entry.targetTopicId
          : (inbox ?? entry.targetTopicId),
      }));
      if (await applyMoves(items)) setUndo(null);
    },
  };
}

async function readSnapshot(api: ApiClient, signal: AbortSignal): Promise<Snapshot> {
  async function pages<T>(path: string) {
    const rows: T[] = [];
    let cursor: string | null = null;
    const seen = new Set<string>();
    do {
      const { data }: { data: Page<T> } = await api.request<Page<T>>(
        path + (cursor ? '?cursor=' + encodeURIComponent(cursor) : ''),
        'GET',
        undefined,
        undefined,
        signal,
      );
      rows.push(...data.items);
      cursor = data.nextCursor;
      if (cursor && seen.has(cursor)) throw new Error('Seitenfolge wiederholt.');
      if (cursor) seen.add(cursor);
    } while (cursor);
    return rows;
  }
  const [topics, tags, documents] = await Promise.all([
    api.request<TopicItem[]>('/api/v1/topics', 'GET', undefined, undefined, signal),
    pages<TagItem>('/api/v1/tags'),
    pages<DocumentItem>('/api/v1/documents'),
  ]);
  return { topics: topics.data, tags, documents };
}

export type PersistentWorkspace = ReturnType<typeof usePersistentWorkspace>;
