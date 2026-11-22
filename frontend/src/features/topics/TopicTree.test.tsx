import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { demoTopics } from './demoTopics';
import { TopicTree } from './TopicTree';
import styles from './TopicTree.module.css';

describe('TopicTree', () => {
  it('zeigt den vorgegebenen Themenbaum an', () => {
    render(<TopicTree topics={demoTopics} selectedTopicId="unassigned" onSelect={vi.fn()} />);

    expect(screen.getByRole('navigation', { name: 'Themen' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Nicht zugeordnet' })).toHaveAttribute(
      'aria-current',
      'page',
    );
    expect(screen.getByRole('button', { name: 'Lenkungskreis' })).toBeInTheDocument();
  });

  it('meldet die Auswahl eines Themas', async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(<TopicTree topics={demoTopics} selectedTopicId="unassigned" onSelect={onSelect} />);

    await user.click(screen.getByRole('button', { name: 'Kunde Beta' }));
    expect(onSelect).toHaveBeenCalledTimes(1);
    expect(onSelect).toHaveBeenCalledWith('beta');
  });

  it('klappt Unterthemen ohne Verlust der Auswahl zu', async () => {
    const user = userEvent.setup();
    render(<TopicTree topics={demoTopics} selectedTopicId="unassigned" onSelect={vi.fn()} />);

    await user.click(screen.getByRole('button', { name: 'Kunde Alpha: Unterthemen einklappen' }));
    expect(screen.queryByRole('button', { name: 'Lenkungskreis' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Nicht zugeordnet' })).toHaveAttribute(
      'aria-current',
      'page',
    );

    await user.click(screen.getByRole('button', { name: 'Kunde Alpha: Unterthemen aufklappen' }));
    expect(screen.getByRole('button', { name: 'Lenkungskreis' })).toBeInTheDocument();
  });
});

it('zählt Dokumente rekursiv und öffnet das Menü per Tastatur', async () => {
  const user = userEvent.setup();
  render(
    <TopicTree
      topics={demoTopics}
      selectedTopicId="unassigned"
      onSelect={vi.fn()}
      documentTopicIds={['alpha-steering', 'alpha-project']}
    />,
  );
  expect(screen.getByRole('button', { name: 'Kunden' })).toHaveTextContent('2');
  const beta = screen.getByRole('button', { name: 'Kunde Beta' });
  beta.focus();
  await user.keyboard('{Shift>}{F10}{/Shift}');
  expect(screen.getByRole('menu', { name: 'Kontextmenü Kunde Beta' })).toBeInTheDocument();
  await user.keyboard('{Escape}');
  expect(beta).toHaveFocus();
});

it('meldet beim Drop die Quell- und Zielthemen an den Verschiebe-Handler', () => {
  const onMove = vi.fn();
  render(
    <TopicTree
      topics={demoTopics}
      selectedTopicId="unassigned"
      onSelect={vi.fn()}
      onMove={onMove}
    />,
  );
  const dataTransfer = { types: ['application/x-minidoc-topic'], getData: () => 'alpha' };
  fireEvent.drop(screen.getByRole('button', { name: 'Kunde Beta' }).parentElement!, {
    dataTransfer,
  });
  expect(onMove).toHaveBeenCalledWith('alpha', 'beta');
});

function renderDragTree() {
  const onMove = vi.fn();
  render(
    <TopicTree
      topics={demoTopics}
      selectedTopicId="unassigned"
      onSelect={vi.fn()}
      onMove={onMove}
    />,
  );
  const source = screen.getByRole('button', { name: 'Kunde Alpha' }).parentElement!;
  const target = screen.getByRole('button', { name: 'Kunde Beta' }).parentElement!;
  const dataTransfer = { setData: vi.fn(), getData: vi.fn(() => 'alpha') };
  fireEvent.dragStart(source, { dataTransfer });
  return { source, target, dataTransfer, onMove };
}

it('markiert ein gültiges Ziel ohne getData während dragover und behält es bei internen Übergängen', () => {
  const { target, dataTransfer } = renderDragTree();
  fireEvent.dragOver(target, { dataTransfer });
  expect(dataTransfer.getData).not.toHaveBeenCalled();
  expect(target).toHaveClass(styles.drop);
  fireEvent(
    target,
    new MouseEvent('dragleave', { bubbles: true, relatedTarget: target.querySelector('span') }),
  );
  expect(target).toHaveClass(styles.drop);
  fireEvent(target, new MouseEvent('dragleave', { bubbles: true, relatedTarget: document.body }));
  expect(target).not.toHaveClass(styles.drop);
});

it.each(['Nicht zugeordnet', 'Kunde Alpha', 'Lenkungskreis'])(
  'markiert das ungültige Ziel %s nicht',
  (name) => {
    const { target, dataTransfer } = renderDragTree();
    fireEvent.dragOver(target, { dataTransfer });
    const invalid = screen.getByRole('button', { name }).parentElement!;
    expect(fireEvent.dragOver(invalid, { dataTransfer })).toBe(true);
    expect(invalid).not.toHaveClass(styles.drop);
    expect(target).not.toHaveClass(styles.drop);
  },
);

it.each(['dragend', 'escape', 'outside-drop', 'blur'])(
  'entfernt die Markierung bei %s',
  (reason) => {
    const { source, target, dataTransfer } = renderDragTree();
    fireEvent.dragOver(target, { dataTransfer });
    expect(target).toHaveClass(styles.drop);
    if (reason === 'dragend') fireEvent.dragEnd(source);
    if (reason === 'escape') fireEvent.keyDown(document, { key: 'Escape' });
    if (reason === 'outside-drop') fireEvent.drop(document.body);
    if (reason === 'blur') fireEvent.blur(window);
    expect(target).not.toHaveClass(styles.drop);
    fireEvent.dragOver(target, { dataTransfer });
    expect(target).not.toHaveClass(styles.drop);
  },
);

it('entfernt die Markierung bei Drop und meldet die Verschiebung', () => {
  const { target, dataTransfer, onMove } = renderDragTree();
  fireEvent.dragOver(target, { dataTransfer });
  fireEvent.drop(target, { dataTransfer });
  expect(target).not.toHaveClass(styles.drop);
  expect(onMove).toHaveBeenCalledWith('alpha', 'beta');
});
