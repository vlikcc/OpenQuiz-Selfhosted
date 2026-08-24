import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, beforeAll, vi } from 'vitest';
import i18n from '../i18n';

// The detector would otherwise pick the language up from the environment and
// make the rendered copy differ between machines.
beforeAll(() => i18n.changeLanguage('tr'));

afterEach(() => {
  cleanup();
  localStorage.clear();
  vi.clearAllMocks();
});
