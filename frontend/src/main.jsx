import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { GoogleOAuthProvider } from '@react-oauth/google';
import { GOOGLE_CLIENT_ID } from './config/constants';
import App from './App.jsx';
import './index.css';
import './i18n';

const root = createRoot(document.getElementById('root'));

const tree = (
  <StrictMode>
    <App />
  </StrictMode>
);

root.render(
  GOOGLE_CLIENT_ID
    ? <GoogleOAuthProvider clientId={GOOGLE_CLIENT_ID}>{tree}</GoogleOAuthProvider>
    : tree,
);
