import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import Dashboard from './Dashboard';
import { pollService } from '../services/pollService';
import { POLL_TYPE_VALUE } from '../config/constants';

vi.mock('../services/pollService', () => ({
  pollService: { list: vi.fn(), get: vi.fn(), remove: vi.fn() },
}));

const summary = (n) => ({
  id: `poll-${n}`,
  title: `Yarışma ${n}`,
  type: POLL_TYPE_VALUE.contest,
  creatorEmail: 'creator@openquiz.test',
  questionCount: 3,
});

const page = (items, hasMore) => ({ items, page: 1, pageSize: 24, totalCount: 30, hasMore });

function renderDashboard() {
  return render(
    <Dashboard
      onNavigate={vi.fn()}
      user={{ email: 'creator@openquiz.test' }}
      showToast={vi.fn()}
      isAdmin={false}
      isAuthorized
      composer={null}
      setComposer={vi.fn()}
    />,
  );
}

describe('poll list paging', () => {
  beforeEach(() => vi.clearAllMocks());

  it('appends the next page instead of replacing the visible one', async () => {
    const user = userEvent.setup();
    // Keyed on the requested page so the test also proves which page was asked for.
    pollService.list.mockImplementation(({ page: requested }) =>
      Promise.resolve(
        requested === 1 ? page([summary(1), summary(2)], true) : page([summary(3)], false),
      ),
    );

    renderDashboard();

    expect(await screen.findByText('Yarışma 1')).toBeInTheDocument();
    expect(screen.queryByText('Yarışma 3')).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /Daha fazla/ }));

    expect(await screen.findByText('Yarışma 3')).toBeInTheDocument();
    expect(screen.getByText('Yarışma 1')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Daha fazla/ })).not.toBeInTheDocument();
  });

  it('does not offer more pages when the first one is the whole list', async () => {
    pollService.list.mockResolvedValue(page([summary(1)], false));

    renderDashboard();

    expect(await screen.findByText('Yarışma 1')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Daha fazla/ })).not.toBeInTheDocument();
  });

  it('shows the question count the summary reports', async () => {
    pollService.list.mockResolvedValue(page([summary(1)], false));

    renderDashboard();

    expect(await screen.findByText('3 Soru')).toBeInTheDocument();
  });
});
