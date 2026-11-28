import type { DocumentSummary } from './document.types';
import type { TagDefinition } from './tags';

/** D6 normalization v1: ECMAScript trim and German lowercase, without accent folding. */
function tagNameKey(name: string): string {
  return name.trim().toLocaleLowerCase('de');
}

export function validateTag(
  candidate: Omit<TagDefinition, 'id'>,
  tags: readonly TagDefinition[],
  id?: string,
): string {
  const key = tagNameKey(candidate.name);
  if (!key) return 'Bitte einen Namen eingeben.';
  if (tags.some((tag) => tag.id !== id && tagNameKey(tag.name) === key))
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
