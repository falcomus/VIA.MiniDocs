import { render, screen } from '@testing-library/react';
import { expect, it } from 'vitest';
import { DemoInkPage } from './DemoInkPage';
import { demoDocuments } from './demoDocuments';

it('verwendet das KU-Papier, die Handschrift und den Seitenkopf', () => {
  render(<DemoInkPage document={demoDocuments[0]} page={1} label="Notizseite" />);
  const svg = screen.getByRole('img', { name: 'Notizseite' });
  expect(svg).toHaveAttribute('viewBox', '0 0 210 297');
  expect(svg.querySelector('rect')).toHaveAttribute('fill', '#fdfcf7');
  expect(svg.querySelector('path')).toHaveAttribute('stroke', '#dfe5ee');
  expect(svg.querySelector('g')).toHaveAttribute(
    'font-family',
    "'Segoe Print','Ink Free','Bradley Hand','Chalkboard SE','Comic Sans MS','Comic Neue',cursive",
  );
  expect(svg).toHaveTextContent('18.11.26');
  expect(svg).toHaveTextContent('Besprechung');
  expect(svg).toHaveTextContent('S. 1');
  expect(svg).toHaveTextContent('Projektstart: Nächste Schritte');
});

it('liefert deterministische Zeilenpositionen und getrennte Inhalte je Seite', () => {
  const { rerender } = render(
    <DemoInkPage document={demoDocuments[0]} page={1} label="Notizseite" />,
  );
  const getLines = () =>
    Array.from(screen.getByRole('img', { name: 'Notizseite' }).querySelectorAll('g > text')).map(
      (element) => [
        element.getAttribute('x'),
        element.getAttribute('y'),
        element.getAttribute('transform'),
        element.textContent,
      ],
    );
  const first = getLines();
  expect(first[3]).toEqual([
    '14.0',
    '44.0',
    'rotate(0.64 14.0 44.0)',
    'Besprechungsnotizen – Eingang',
  ]);
  rerender(<DemoInkPage document={demoDocuments[0]} page={1} label="Notizseite" />);
  expect(getLines()).toEqual(first);
  rerender(<DemoInkPage document={demoDocuments[0]} page={2} label="Notizseite" />);
  expect(screen.getByRole('img', { name: 'Notizseite' })).toHaveTextContent(
    'Frühjahr: Lösung prüfen und',
  );
  expect(screen.getByRole('img', { name: 'Notizseite' })).not.toHaveTextContent('Projektstart');
});

it('ändert die Bildnotizen nicht durch Transkriptbearbeitung', () => {
  const edited = {
    ...demoDocuments[0],
    transcript: { edited: true, pages: [{ page: 1, text: 'Neuer Transkripttext' }] },
  };
  render(<DemoInkPage document={edited} page={1} label="Notizseite" />);
  const image = screen.getByRole('img', { name: 'Notizseite' });
  expect(image).toHaveTextContent('Projektstart');
  expect(image).not.toHaveTextContent('Neuer Transkripttext');
});
