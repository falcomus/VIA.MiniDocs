import { calendarValue } from './documentOperations';
const formatter = new Intl.DateTimeFormat('de-DE', {
  timeZone: 'UTC',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
});
/** Returns KU's missing-date label for absent or invalid calendar input. */
export function formatDocumentDate(value: string | null): string {
  const time = calendarValue(value);
  return time === null ? 'ohne Datum' : formatter.format(new Date(time));
}
