export type TopicId = string;

export interface Topic {
  readonly id: TopicId;
  readonly etag?: string;
  readonly parentId: TopicId | null;
  readonly name: string;
  readonly systemKey?: 'UNASSIGNED';
}
