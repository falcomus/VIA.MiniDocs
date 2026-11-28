import { act, fireEvent, render, renderHook, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { ApiClient } from '../auth/api';
import { usePersistentWorkspace } from './usePersistentWorkspace';
import { TagDialog } from './TagDialog';
import { useSearchState } from '../search/useSearchState';
import { useTopicDrag } from '../topics/useTopicDrag';

const tag = { id: 'tag-1', name: 'Projekt', description: '', color: '#225b79', etag: '"1"' };
const topic = { id: 'topic-1', name: 'Thema', parentId: null, etag: '"1"' };

afterEach(() => vi.unstubAllGlobals());

function json(data: unknown, status = 200) {
  return new Response(JSON.stringify(data), { status });
}

function setup() {
  let tags = [{ tag, etag: tag.etag }];
  let readsFail = false;
  let failAfterWrite = false;
  let writeStatus = 200;
  let batchStatus = 200;
  const batches: unknown[] = [];
  const writes: { path: string; method: string; etag: string | null }[] = [];
  vi.stubGlobal(
    'fetch',
    vi.fn(async (path: string, init?: RequestInit) => {
      const method = init?.method ?? 'GET';
      if (path.endsWith('/antiforgery')) return json({ requestToken: 'csrf' });
      if (method === 'GET') {
        if (readsFail) return json({ code: 'dependency_unavailable' }, 503);
        if (path.endsWith('/topics')) return json([{ topic, etag: topic.etag }]);
        return json({ items: path.endsWith('/tags') ? tags : [], nextCursor: null });
      }
      writes.push({ path, method, etag: new Headers(init?.headers).get('If-Match') });
      if (path.endsWith('/batch-tags')) {
        batches.push(JSON.parse(String(init?.body)));
        return json({ code: 'version_mismatch' }, batchStatus);
      }
      if (failAfterWrite) readsFail = true;
      return writeStatus === 200 ? json(tag) : json({ code: 'version_mismatch' }, writeStatus);
    }),
  );
  const api = new ApiClient();
  const recheck = vi.fn();
  const hook = renderHook(() => usePersistentWorkspace(api, 'test-workspace', recheck));
  return {
    ...hook,
    writes,
    batches,
    removeTag: () => {
      tags = [];
    },
    failRefreshAfterWrite: () => {
      failAfterWrite = true;
    },
    restoreReads: () => {
      readsFail = false;
      failAfterWrite = false;
    },
    setWriteStatus: (status: number) => {
      writeStatus = status;
    },
    setBatchStatus: (status: number) => {
      batchStatus = status;
    },
  };
}

it('verwendet die Server-Tag-ID und die ursprünglichen ETags und meldet Teilfehler ohne Wiederholung', async () => {
  const test = setup();
  await waitFor(() => expect(test.result.current.ready).toBe(true));
  test.setBatchStatus(412);
  const selected = ['first', 'second'].map((id) => ({
    id,
    etag: '"7"',
    title: id,
    topicId: 'topic-1',
    date: null,
    pages: null,
    kind: 'Notiz' as const,
  }));
  await act(async () => {
    expect(
      await test.result.current.saveTag({ ...tag, id: 'browser-only' }, 'create', selected),
    ).toBe('assignment_unconfirmed');
  });
  expect(test.batches).toEqual([
    {
      items: [
        { id: 'first', etag: '"7"' },
        { id: 'second', etag: '"7"' },
      ],
      tagId: tag.id,
      on: true,
    },
  ]);
  expect(test.writes.map(({ path }) => path)).toEqual([
    '/api/v1/tags',
    '/api/v1/documents/batch-tags',
  ]);
  expect(test.result.current.error).toContain('zwischenzeitlich geändert');
});

it('sendet bei einem zwischenzeitlich entfernten Tag weiter PATCH mit der gelesenen Version', async () => {
  const test = setup();
  await waitFor(() => expect(test.result.current.ready).toBe(true));
  const original = test.result.current.tags[0];
  test.removeTag();
  await act(async () => {
    await test.result.current.reload();
  });
  expect(test.result.current.tags).toEqual([]);
  test.setWriteStatus(404);
  await act(async () => {
    expect(await test.result.current.saveTag(original, 'update')).toBeNull();
  });
  expect(test.writes).toEqual([{ path: '/api/v1/tags/tag-1', method: 'PATCH', etag: '"1"' }]);
});

it('sperrt nach bestätigtem Schreiben und fehlgeschlagenem Reload weitere Writes bis zur Aktualisierung', async () => {
  const test = setup();
  await waitFor(() => expect(test.result.current.ready).toBe(true));
  test.failRefreshAfterWrite();
  await act(async () => {
    expect(await test.result.current.saveTag(tag, 'update')).toBe(true);
  });
  expect(test.result.current.error).toContain('Änderung gespeichert');
  await act(async () => {
    expect(await test.result.current.saveTag(tag, 'update')).toBeNull();
  });
  expect(test.writes).toHaveLength(1);
  test.restoreReads();
  await act(async () => {
    await test.result.current.reload();
  });
  await act(async () => {
    expect(await test.result.current.saveTag(tag, 'update')).toBe(true);
  });
  expect(test.writes).toHaveLength(2);
});

it('behält beim Tag-Edit die ursprüngliche Version, blockiert weitere Aktionen und stellt Fokus wieder her', async () => {
  let finish: (value: boolean) => void = () => undefined;
  const pending = new Promise<boolean>((resolve) => {
    finish = resolve;
  });
  const onSave = vi.fn(() => pending);
  const onClose = vi.fn();
  const props = {
    dialog: { kind: 'manage' } as const,
    documents: [],
    visibleIds: [],
    onAssign: vi.fn(),
    onSave,
    onDelete: vi.fn(),
    onManage: vi.fn(),
    onClose,
  };
  const { rerender } = render(<TagDialog {...props} tags={[tag]} />);
  fireEvent.click(screen.getByRole('button', { name: 'Projekt bearbeiten' }));
  fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Mein Entwurf' } });
  rerender(<TagDialog {...props} tags={[{ ...tag, etag: '"2"', name: 'Andere Version' }]} />);
  fireEvent.click(screen.getByRole('button', { name: 'Speichern' }));
  expect(onSave).toHaveBeenCalledWith({ ...tag, name: 'Mein Entwurf' }, 'update');
  expect(screen.getByRole('button', { name: 'Speichern' })).toBeDisabled();
  expect(screen.getByRole('button', { name: 'Fertig' })).toBeDisabled();
  fireEvent.keyDown(screen.getByRole('dialog'), { key: 'Escape' });
  expect(onClose).not.toHaveBeenCalled();
  await act(async () => {
    finish(true);
  });
  expect(screen.getByRole('button', { name: 'Fertig' })).toHaveFocus();
  expect(screen.queryByLabelText('Name')).not.toBeInTheDocument();
});

it('verwendet beim Löschen die im Bestätigungsdialog gelesene Tagversion', () => {
  const onDelete = vi.fn();
  const props = {
    dialog: { kind: 'manage' } as const,
    documents: [],
    visibleIds: [],
    onAssign: vi.fn(),
    onSave: vi.fn(),
    onDelete,
    onManage: vi.fn(),
    onClose: vi.fn(),
  };
  const { rerender } = render(<TagDialog {...props} tags={[tag]} />);
  fireEvent.click(screen.getByRole('button', { name: 'Projekt löschen' }));
  rerender(<TagDialog {...props} tags={[{ ...tag, etag: '"2"' }]} />);
  fireEvent.click(screen.getByRole('button', { name: 'Endgültig löschen' }));
  expect(onDelete).toHaveBeenCalledWith(tag.id, '"1"');
  expect(screen.getByRole('button', { name: 'Fertig' })).toHaveFocus();
});

it('entfernt einen in einem anderen Tab gelöschten Tag aus den effektiven Suchkriterien', () => {
  const { result, rerender } = renderHook(({ ids }) => useSearchState(ids), {
    initialProps: { ids: [tag.id] },
  });
  act(() => result.current.setCriteria((previous) => ({ ...previous, tagIds: [tag.id] })));
  expect(result.current.active).toBe(true);
  rerender({ ids: [] });
  expect(result.current.criteria.tagIds).toEqual([]);
  expect(result.current.active).toBe(false);
});

it('bindet einen Themen-Drag an die beim Start gelesene Version', () => {
  const onMove = vi.fn();
  const onReject = vi.fn();
  const target = { id: 'target', name: 'Ziel', parentId: null, etag: '"1"' };
  const { result, rerender } = renderHook(({ topics }) => useTopicDrag(topics, onMove, onReject), {
    initialProps: { topics: [topic, target] },
  });
  const transfer = { setData: vi.fn(), getData: () => topic.id } as unknown as DataTransfer;
  act(() => {
    result.current.start(topic.id, transfer);
  });
  rerender({ topics: [{ ...topic, etag: '"2"' }, target] });
  act(() => {
    result.current.drop(target.id, transfer);
  });
  expect(onMove).toHaveBeenCalledWith(topic.id, target.id, '"1"');
});
