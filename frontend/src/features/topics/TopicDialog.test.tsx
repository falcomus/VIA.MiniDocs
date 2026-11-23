import { fireEvent, render, screen } from '@testing-library/react';
import { expect, it, vi } from 'vitest';
import { TopicDialog } from './TopicDialog';
import { topicDeleteBlockReason } from './topicUtils';
import { demoTopics } from './demoTopics';

it('sperrt die Löschbestätigung, wenn ein seit Dialogöffnung belegtes Thema aktualisiert wird', () => {
  const onSubmit = vi.fn(),
    onCancel = vi.fn();
  const id = 'alpha-steering';
  const props = {
    dialog: { kind: 'delete' as const, id },
    topics: demoTopics,
    error: '',
    onCancel,
    onSubmit,
  };
  const { rerender } = render(
    <TopicDialog {...props} deleteReason={topicDeleteBlockReason(demoTopics, [], id)} />,
  );
  expect(screen.getByRole('button', { name: 'Löschen' })).toBeEnabled();
  rerender(
    <TopicDialog
      {...props}
      deleteReason={topicDeleteBlockReason(demoTopics, [{ topicId: id }], id)}
    />,
  );
  expect(screen.getByRole('dialog')).toHaveTextContent('enthält Dokumente');
  expect(screen.getByRole('button', { name: 'Löschen' })).toBeDisabled();
  fireEvent.click(screen.getByRole('button', { name: 'Löschen' }));
  expect(onSubmit).not.toHaveBeenCalled();
  fireEvent.keyDown(screen.getByRole('dialog'), { key: 'Escape' });
  expect(onCancel).toHaveBeenCalledOnce();
});
