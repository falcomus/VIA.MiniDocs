import type { TagDefinition, TagId } from './tags';
import styles from './TagChips.module.css';
export function TagChips({
  ids,
  tags,
}: {
  readonly ids?: readonly TagId[];
  readonly tags: readonly TagDefinition[];
}) {
  const assigned = tags.filter((tag) => ids?.includes(tag.id));
  return assigned.length ? (
    <span className={styles.chips}>
      {assigned.map((tag) => (
        <span
          className={styles.chip}
          key={tag.id}
          title={tag.description}
          style={{ borderColor: tag.color }}
        >
          <span className={styles.dot} style={{ backgroundColor: tag.color }} />
          {tag.name}
        </span>
      ))}
    </span>
  ) : null;
}
