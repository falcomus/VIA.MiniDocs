import type { DocumentSummary } from './document.types';
import styles from './DemoInkPage.module.css';

/** Deterministic pseudo-random sequence from KU's original pageSVG implementation. */
function rng(seed: number): () => number {
  return () => {
    seed |= 0;
    seed = (seed + 0x6d2b79f5) | 0;
    let value = Math.imul(seed ^ (seed >>> 15), 1 | seed);
    value = (value + Math.imul(value ^ (value >>> 7), 61 | value)) ^ value;
    return ((value ^ (value >>> 14)) >>> 0) / 4294967296;
  };
}

function hash(value: string): number {
  let result = 2166136261;
  for (const character of value) {
    result ^= character.charCodeAt(0);
    result = Math.imul(result, 16777619);
  }
  return result >>> 0;
}

/** Preserve KU's word wrapping, including blank-line handling. */
function wrap(value: string, limit: number): string[] {
  const result: string[] = [];
  let current = '';
  for (const word of String(value).split(/\s+/)) {
    if (!word) continue;
    if ((current + ' ' + word).trim().length > limit && current) {
      result.push(current);
      current = word;
    } else {
      current = (current + ' ' + word).trim();
    }
  }
  if (current) result.push(current);
  return result.length ? result : [''];
}

const handwritingFont =
  "'Segoe Print','Ink Free','Bradley Hand'," +
  "'Chalkboard SE','Comic Sans MS','Comic Neue',cursive";

const grid = (() => {
  let path = '';
  for (let y = 10; y < 297; y += 5) path += `M0 ${y}H210`;
  for (let x = 5; x < 210; x += 5) path += `M${x} 0V297`;
  return path;
})();

/** KU's SVG paper, header and handwritten line layout; the demo ink is independent of edits. */
export function DemoInkPage({
  document,
  page,
  label,
}: {
  readonly document: DocumentSummary;
  readonly page: number;
  readonly label: string;
}) {
  const index = page - 1;
  const random = rng(hash(`${document.id}:${index}`));
  const lines =
    document.demoInk?.pages[index] ??
    (index === 0
      ? (document.demoPreview?.lines ?? [
          document.title,
          'Demo-Notizen',
          'Besprechung und nächste Schritte',
        ])
      : []);
  const handwritten: {
    readonly x: string;
    readonly y: string;
    readonly rotation: string;
    readonly text: string;
  }[] = [];
  let y = 44;
  for (const line of lines) {
    for (const segment of wrap(line, 31)) {
      if (y > 286) break;
      const x = (14 + random() * 3).toFixed(1);
      const position = y.toFixed(1);
      const rotation = ((random() - 0.5) * 1.4).toFixed(2);
      handwritten.push({ x, y: position, rotation, text: segment });
      y += 12 + random() * 1.3;
    }
    y += 1.5;
  }
  const shortDate = document.date
    ? `${document.date.slice(8, 10)}.${document.date.slice(5, 7)}.${document.date.slice(2, 4)}`
    : '';
  return (
    <svg
      className={styles.page}
      viewBox="0 0 210 297"
      role="img"
      aria-label={label}
      xmlns="http://www.w3.org/2000/svg"
    >
      <title>{label}</title>
      <rect width="210" height="297" fill="#fdfcf7" />
      <path d={grid} stroke="#dfe5ee" strokeWidth=".3" />
      <g fontFamily={handwritingFont} fill="#22398a" fontSize="9">
        <text x="12" y="20">
          {shortDate}
        </text>
        <text x="105" y="20" textAnchor="middle">
          {document.demoInk?.head ?? document.title}
        </text>
        <text x="198" y="20" textAnchor="end">
          S. {page}
        </text>
        <path d="M10 24.5H200" stroke="#22398a" strokeWidth=".5" />
        {handwritten.map((line, i) => (
          <text
            key={i}
            x={line.x}
            y={line.y}
            transform={`rotate(${line.rotation} ${line.x} ${line.y})`}
          >
            {line.text}
          </text>
        ))}
      </g>
    </svg>
  );
}
