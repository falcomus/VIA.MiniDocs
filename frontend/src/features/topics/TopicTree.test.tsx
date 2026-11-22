import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { demoTopics } from './demoTopics';
import { TopicTree } from './TopicTree';
import { useTopicDrag } from './useTopicDrag';
import type { TopicId } from './topic.types';
import styles from './TopicTree.module.css';

afterEach(() => vi.restoreAllMocks());

function DragTree({
  onMove,
  onReject,
}: {
  onMove: (id: TopicId, parentId: TopicId | null) => void;
  onReject: () => void;
}) {
  const drag = useTopicDrag(demoTopics, onMove, onReject);
  return (
    <TopicTree topics={demoTopics} selectedTopicId="unassigned" onSelect={vi.fn()} drag={drag} />
  );
}

describe('TopicTree', () => {
  it('zeigt den vorgegebenen Themenbaum an', () => {
    render(<TopicTree topics={demoTopics} selectedTopicId="unassigned" onSelect={vi.fn()} />);

    expect(screen.getByRole('navigation', { name: 'Themen' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Alle zugeordnet' })).toHaveAttribute(
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
    expect(screen.getByRole('button', { name: 'Alle zugeordnet' })).toHaveAttribute(
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
  const { target, dataTransfer, onMove } = renderDragTree();
  expect(dataTransfer.setData).toHaveBeenCalledWith('application/x-minidoc-topic', 'alpha');
  fireEvent.drop(target, { dataTransfer });
  expect(onMove).toHaveBeenCalledTimes(1);
  expect(onMove).toHaveBeenCalledWith('alpha', 'beta');
});

function renderDragTree() {
  const onMove = vi.fn();
  const onReject = vi.fn();
  const { unmount } = render(<DragTree onMove={onMove} onReject={onReject} />);
  const source = screen.getByRole('button', { name: 'Kunde Alpha' }).parentElement!;
  const target = screen.getByRole('button', { name: 'Kunde Beta' }).parentElement!;
  const dataTransfer = { setData: vi.fn(), getData: vi.fn(() => 'alpha') };
  fireEvent.dragStart(source, { dataTransfer });
  return { source, target, dataTransfer, onMove, onReject, unmount };
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

it.each(['Alle zugeordnet', 'Kunde Alpha', 'Lenkungskreis'])(
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
    const { source, target, dataTransfer, onMove, onReject } = renderDragTree();
    fireEvent.dragOver(target, { dataTransfer });
    expect(target).toHaveClass(styles.drop);
    if (reason === 'dragend') fireEvent.dragEnd(source);
    if (reason === 'escape') fireEvent.keyDown(document, { key: 'Escape' });
    if (reason === 'outside-drop') fireEvent.drop(document.body);
    if (reason === 'blur') fireEvent.blur(window);
    expect(target).not.toHaveClass(styles.drop);
    fireEvent.dragOver(target, { dataTransfer });
    expect(target).not.toHaveClass(styles.drop);
    fireEvent.drop(target, { dataTransfer });
    expect(onMove).not.toHaveBeenCalled();
    expect(onReject).toHaveBeenCalledOnce();
  },
);

it('entfernt die Markierung bei Drop und meldet die Verschiebung', () => {
  const { target, dataTransfer, onMove } = renderDragTree();
  fireEvent.dragOver(target, { dataTransfer });
  fireEvent.drop(target, { dataTransfer });
  expect(target).not.toHaveClass(styles.drop);
  expect(onMove).toHaveBeenCalledWith('alpha', 'beta');
});

it.each(['beta', ''])(
  'weist abweichende oder fehlende Nutzdaten "%s" zurück und beendet die Sitzung',
  (payload) => {
    const { target, dataTransfer, onMove, onReject } = renderDragTree();
    fireEvent.dragOver(target, { dataTransfer });
    dataTransfer.getData.mockReturnValue(payload);
    fireEvent.drop(target, { dataTransfer });
    expect(onMove).not.toHaveBeenCalled();
    expect(onReject).toHaveBeenCalledOnce();
    expect(target).not.toHaveClass(styles.drop);
    dataTransfer.getData.mockReturnValue('alpha');
    fireEvent.drop(target, { dataTransfer });
    expect(onMove).not.toHaveBeenCalled();
    expect(onReject).toHaveBeenCalledTimes(2);
  },
);

it('weist einen Drop ohne lokalen Dragstart zurück', () => {
  const onMove = vi.fn();
  const onReject = vi.fn();
  render(<DragTree onMove={onMove} onReject={onReject} />);
  const target = screen.getByRole('button', { name: 'Kunde Beta' }).parentElement!;
  const dataTransfer = { getData: vi.fn(() => 'alpha') };
  expect(fireEvent.dragOver(target, { dataTransfer })).toBe(true);
  fireEvent.drop(target, { dataTransfer });
  expect(onMove).not.toHaveBeenCalled();
  expect(onReject).toHaveBeenCalledOnce();
  expect(dataTransfer.getData).not.toHaveBeenCalled();
});

it.each(['Alle zugeordnet', 'Kunde Alpha', 'Lenkungskreis'])(
  'weist einen Drop auf %s ohne Move-Callback zurück',
  (name) => {
    const { target, dataTransfer, onMove, onReject } = renderDragTree();
    fireEvent.dragOver(target, { dataTransfer });
    fireEvent.drop(screen.getByRole('button', { name }).parentElement!, { dataTransfer });
    expect(onMove).not.toHaveBeenCalled();
    expect(onReject).toHaveBeenCalledOnce();
    expect(target).not.toHaveClass(styles.drop);
    fireEvent.dragOver(target, { dataTransfer });
    expect(target).not.toHaveClass(styles.drop);
  },
);

it('verbraucht auch eine gültige Drag-Sitzung nur einmal', () => {
  const { target, dataTransfer, onMove, onReject } = renderDragTree();
  fireEvent.drop(target, { dataTransfer });
  fireEvent.drop(target, { dataTransfer });
  expect(onMove).toHaveBeenCalledExactlyOnceWith('alpha', 'beta');
  expect(onReject).toHaveBeenCalledOnce();
});

it('entfernt beim Unmount dieselben globalen Drag-Listener', () => {
  const addDocument = vi.spyOn(document, 'addEventListener');
  const removeDocument = vi.spyOn(document, 'removeEventListener');
  const addWindow = vi.spyOn(window, 'addEventListener');
  const removeWindow = vi.spyOn(window, 'removeEventListener');
  const { unmount } = renderDragTree();
  const listeners = addDocument.mock.calls.filter(([type]) =>
    ['drop', 'dragend', 'keydown'].includes(type),
  );
  const blurListeners = addWindow.mock.calls.filter(([type]) => type === 'blur');
  expect(listeners).toHaveLength(3);
  expect(blurListeners).toHaveLength(1);
  unmount();
  for (const [type, listener] of listeners)
    expect(removeDocument).toHaveBeenCalledWith(type, listener);
  for (const [type, listener] of blurListeners)
    expect(removeWindow).toHaveBeenCalledWith(type, listener);
});
it('leitet den Systemstatus aus direkter Zuordnung ab und bewahrt ID, Icon und Auswahl', () => {
  const topics = [...demoTopics, { id: 'child', parentId: 'unassigned', name: 'Unterthema' }];
  const onSelect = vi.fn();
  const { rerender } = render(
    <TopicTree
      topics={topics}
      selectedTopicId="unassigned"
      onSelect={onSelect}
      documentTopicIds={['child']}
    />,
  );
  const complete = screen.getByRole('button', { name: 'Alle zugeordnet' });
  expect(complete).toHaveAttribute('data-topic-id', 'unassigned');
  expect(complete.querySelector('svg')).toHaveClass(styles.completeIcon);
  fireEvent.click(complete);
  expect(onSelect).toHaveBeenCalledWith('unassigned');
  rerender(
    <TopicTree
      topics={topics}
      selectedTopicId="unassigned"
      onSelect={onSelect}
      documentTopicIds={['child', 'unassigned']}
    />,
  );
  const inbox = screen.getByRole('button', { name: 'Nicht zugeordnet' });
  expect(inbox).toHaveAttribute('data-topic-id', 'unassigned');
  expect(inbox.querySelector('svg')).toHaveClass(styles.system);
  expect(inbox).toHaveAttribute('aria-description', '2 Dokumente einschließlich Unterthemen');
});
