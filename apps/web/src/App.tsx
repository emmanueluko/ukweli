import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { Home } from './routes/Home';
import { Landing } from './routes/Landing';
import { LinkSent } from './routes/LinkSent';
import { MyChecks } from './routes/MyChecks';
import { Result } from './routes/Result';
import { SignIn } from './routes/SignIn';
import { SourceDetail } from './routes/SourceDetail';

export function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Landing />} />
        <Route path="/check" element={<Home />} />
        {/* /r/{id} is the share link the server builds into every shareText. */}
        <Route path="/r/:id" element={<Result />} />
        <Route path="/sources/:id" element={<SourceDetail />} />
        <Route path="/sign-in" element={<SignIn />} />
        <Route path="/link-sent" element={<LinkSent />} />
        <Route path="/my-checks" element={<MyChecks />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
