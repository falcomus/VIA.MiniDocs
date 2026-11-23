export type TagId = string;
export interface TagDefinition {
  readonly id: TagId;
  readonly name: string;
  readonly description: string;
  readonly color: string;
}
export const demoTags: readonly TagDefinition[] = [
  { id: 'projekt', name: 'Projekt', description: 'Projektarbeit', color: '#225b79' },
  { id: 'aufgabe', name: 'Aufgabe', description: 'Offene Aufgaben', color: '#b54708' },
  { id: 'idee', name: 'Idee', description: 'Gedanken und Entwürfe', color: '#6941c6' },
];
/** Unknown IDs are omitted rather than displayed as misleading tag names. */
export function tagNames(
  ids: readonly TagId[] | undefined,
  tags: readonly TagDefinition[],
): string[] {
  return (ids ?? []).flatMap((id) => {
    const tag = tags.find((item) => item.id === id);
    return tag ? [tag.name] : [];
  });
}
