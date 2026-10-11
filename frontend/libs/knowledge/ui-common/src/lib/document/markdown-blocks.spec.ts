import { markdownBlocks } from './markdown-blocks';

describe('markdownBlocks', () => {
  it('reads headings, paragraphs and lists without interpreting html', () => {
    const blocks = markdownBlocks('# سياسة\n\nفقرة أولى\nتكملة\n\n- بند 1\n- بند 2\n\n1. أول\n2. ثانٍ\n<b>x</b>');
    expect(blocks).toEqual([
      { kind: 'h', level: 1, text: 'سياسة' },
      { kind: 'p', text: 'فقرة أولى تكملة' },
      { kind: 'ul', items: ['بند 1', 'بند 2'] },
      { kind: 'ol', items: ['أول', 'ثانٍ'] },
      { kind: 'p', text: '<b>x</b>' },
    ]);
  });
});
