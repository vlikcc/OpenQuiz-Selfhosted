import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';
import * as XLSX from 'xlsx';

const DEFAULT_FILENAME = 'report';

function headerRow(labels) {
  return [labels.user, labels.score, labels.time];
}

function scoreRows(scores) {
  return scores.map((s) => [s.userName, s.points, s.totalTimeMs]);
}

function safeName(title) {
  return title || DEFAULT_FILENAME;
}

export function buildScoreReportPdf(title, scores, labels) {
  const doc = new jsPDF();
  doc.text(safeName(title), 14, 16);
  autoTable(doc, {
    head: [['#', ...headerRow(labels)]],
    body: scores.map((s, i) => [i + 1, s.userName, s.points, s.totalTimeMs]),
    startY: 24,
  });
  return doc;
}

export function buildScoreReportWorkbook(scores, labels) {
  const sheet = XLSX.utils.aoa_to_sheet([headerRow(labels), ...scoreRows(scores)]);
  const workbook = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(workbook, sheet, labels.sheet);
  return workbook;
}

export function saveScoreReportPdf(doc, title) {
  doc.save(`${safeName(title)}.pdf`);
}

export function saveScoreReportWorkbook(workbook, title) {
  XLSX.writeFile(workbook, `${safeName(title)}.xlsx`);
}
