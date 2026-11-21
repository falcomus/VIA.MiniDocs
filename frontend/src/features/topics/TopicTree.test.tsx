import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { demoTopics } from './demoTopics';
import { TopicTree } from './TopicTree';

describe('TopicTree', () => {
  it('zeigt den vorgegebenen Themenbaum an', () => {
    render(<TopicTree topics={demoTopics} selectedTopicId="unassigned" onSelect={vi.fn()} />);

    expect(screen.getByRole('navigation', { name: 'Themen' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Nicht zugeordnet' })).toHaveAttribute('aria-current', 'page');
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
    expect(screen.getByRole('button', { name: 'Nicht zugeordnet' })).toHaveAttribute('aria-current', 'page');

    await user.click(screen.getByRole('button', { name: 'Kunde Alpha: Unterthemen aufklappen' }));
    expect(screen.getByRole('button', { name: 'Lenkungskreis' })).toBeInTheDocument();
  });
});
