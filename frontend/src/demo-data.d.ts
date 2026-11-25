declare module 'virtual:demo-data' {
  const value: { records: { dateiName: string; sizeBytes: number; titel: string; seitenzahl: number; datum: string; empfohlenesThema: string; tags: string[]; textart: string }[]; transcripts: Record<string, { fileName: string; pages: { page: number; text: string }[] }> } | null;
  export default value;
}
