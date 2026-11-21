export type TopicId = string;

export interface Topic {
  readonly id: TopicId;
  readonly parentId: TopicId | null;
  readonly name: string;
  readonly systemKey?: 'UNASSIGNED';
}
