import { describe, expect, it } from 'vitest';
import * as XLSX from 'xlsx';
import { buildScoreReportPdf, buildScoreReportWorkbook } from './scoreReport';

const LABELS = { user: 'User', score: 'Score', time: 'Time', sheet: 'Scores' };

const SCORES = [
  { userName: 'Ada', points: 30, totalTimeMs: 1200 },
  { userName: 'Grace', points: 20, totalTimeMs: 1500 },
];

describe('buildScoreReportPdf', () => {
  it('renders the leaderboard table into a non-empty PDF', () => {
    const doc = buildScoreReportPdf('CI Demo Contest', SCORES, LABELS);
    const bytes = doc.output('arraybuffer');

    expect(bytes.byteLength).toBeGreaterThan(0);
    expect(new TextDecoder('latin1').decode(bytes).slice(0, 5)).toBe('%PDF-');
  });

  it('runs the autoTable plugin instead of silently skipping it', () => {
    const doc = buildScoreReportPdf('CI Demo Contest', SCORES, LABELS);

    expect(doc.lastAutoTable).toBeDefined();
    expect(doc.lastAutoTable.body).toHaveLength(SCORES.length);
    expect(doc.lastAutoTable.finalY).toBeGreaterThan(24);
  });

  it('falls back to a generic title when the poll has none', () => {
    expect(() => buildScoreReportPdf('', SCORES, LABELS)).not.toThrow();
  });
});

describe('buildScoreReportWorkbook', () => {
  it('writes a header row followed by one row per score', () => {
    const workbook = buildScoreReportWorkbook(SCORES, LABELS);
    const rows = XLSX.utils.sheet_to_json(workbook.Sheets[LABELS.sheet], { header: 1 });

    expect(workbook.SheetNames).toEqual([LABELS.sheet]);
    expect(rows).toEqual([
      ['User', 'Score', 'Time'],
      ['Ada', 30, 1200],
      ['Grace', 20, 1500],
    ]);
  });
});
