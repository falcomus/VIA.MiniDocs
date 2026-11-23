import type { DocumentSummary } from './document.types';
import type { TagDefinition } from './tags';
export function validateTag(
  candidate: Omit<TagDefinition, 'id'>,
  tags: readonly TagDefinition[],
  id?: string,
): string {
  const name = candidate.name.trim();
  if (!name) return 'Bitte einen Namen eingeben.';
  if (
    tags.some(
      (tag) => tag.id !== id && tag.name.toLocaleLowerCase('de') === name.toLocaleLowerCase('de'),
    )
  )
    return 'Dieser Tagname ist bereits vergeben.';
  if (!/^#[0-9a-f]{6}$/i.test(candidate.color)) return 'Bitte eine gültige Farbe wählen.';
  return '';
}

/** Apply only the requested association, preserving all other document data and tags. */
export function setDocumentTags(
  documents: readonly DocumentSummary[],
  ids: readonly string[],
  tagId: string,
  on: boolean,
): readonly DocumentSummary[] {
  const selected = new Set(ids);
  return documents.map((doc) =>
    selected.has(doc.id)
      ? {
          ...doc,
          tagIds: on
            ? [...new Set([...(doc.tagIds ?? []), tagId])]
            : (doc.tagIds ?? []).filter((id) => id !== tagId),
        }
      : doc,
  );
}
