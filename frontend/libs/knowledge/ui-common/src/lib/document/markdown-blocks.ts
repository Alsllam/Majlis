/** A tiny, safe Markdown reader for drafts: headings, paragraphs, bullet and numbered lists. No HTML is ever injected. */
export type MarkdownBlock = { kind: 'h'; level: number; text: string } | { kind: 'p'; text: string } | { kind: 'ul' | 'ol'; items: string[] };

export function markdownBlocks(markdown: string): MarkdownBlock[] {
  const blocks: MarkdownBlock[] = [];
  let paragraph: string[] = [];
  let list: { kind: 'ul' | 'ol'; items: string[] } | null = null;
  const flush = () => {
    if (paragraph.length) {
      blocks.push({ kind: 'p', text: paragraph.join(' ') });
      paragraph = [];
    }
    if (list) {
      blocks.push(list);
      list = null;
    }
  };
  for (const raw of markdown.split(/\r?\n/)) {
    const line = raw.trimEnd();
    const heading = /^(#{1,6})\s+(.*)$/.exec(line);
    const bullet = /^\s*[-*•]\s+(.*)$/.exec(line);
    const numbered = /^\s*\d+[.)]\s+(.*)$/.exec(line);
    if (!line.trim()) {
      flush();
    } else if (heading) {
      flush();
      blocks.push({ kind: 'h', level: heading[1].length, text: heading[2].trim() });
    } else if (bullet || numbered) {
      const kind = bullet ? 'ul' : 'ol';
      const item = (bullet ?? numbered)?.[1]?.trim() ?? '';
      if (paragraph.length) {
        flush();
      }
      if (!list || list.kind !== kind) {
        if (list) {
          blocks.push(list);
        }
        list = { kind, items: [] };
      }
      list.items.push(item);
    } else {
      if (list) {
        flush();
      }
      paragraph.push(line.trim());
    }
  }
  flush();
  return blocks;
}
