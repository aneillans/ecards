import { BrowserRouter as Router, Routes, Route, Link } from 'react-router-dom';
import { useEffect, useState } from 'react';
import Welcome from './pages/Welcome';
import CreateCard from './pages/CreateCard';
import ViewCard from './pages/ViewCard';
import Admin from './pages/Admin';
import { initAuth, login, logout, isLoggedIn, hasRole } from './auth';
import api from './api';
import './App.css'

function App() {
  const [ready, setReady] = useState(false);
  const [loggedIn, setLoggedIn] = useState(false);
  const [appName, setAppName] = useState('eCards');

  useEffect(() => {
    console.log('App mounting, starting auth initialization...');
    
    const initializeAuth = async () => {
      try {
        console.log('Calling initAuth()...');
        const k = await initAuth();
        console.log('initAuth completed, token:', !!k.token);
        setLoggedIn(!!k.token);
        console.log('Auth ready, logged in:', !!k.token);
      } catch (err) {
        console.error('Auth initialization error:', err);
      } finally {
        console.log('Setting ready to true');
        setReady(true);
      }
    };

    initializeAuth();
    
    // Fetch application config
    api.get('/ecards/config')
      .then(response => {
        if (response.data.appName) {
          setAppName(response.data.appName);
          document.title = response.data.appName;
        }
      })
      .catch(err => console.error('Error fetching config:', err));
  }, []);
  
  return (
    <Router>
      <div className="app">
        <header className="app-header">
          <div className="container">
            <Link to="/" className="logo">
              <h1>🎉 {appName}</h1>
            </Link>
            <nav>
              <Link to="/" className="nav-link">Home</Link>
              {ready && loggedIn && <Link to="/create" className="nav-link">Create Card</Link>}
              {ready && loggedIn && hasRole('admin') && <Link to="/admin" className="nav-link">Admin</Link>}
              {ready && (
                loggedIn
                ? <button className="nav-link" onClick={() => logout()}>Logout</button>
                : <button className="nav-link" onClick={() => login()}>Login</button>
              )}
            </nav>
          </div>
        </header>
        
        <main className="main-content">
          {!ready ? (
            <div className="loading-container" style={{ textAlign: 'center', padding: '2rem' }}>
              <div className="loading">Initializing...</div>
            </div>
          ) : (
            <Routes>
              <Route path="/" element={<Welcome />} />
              <Route path="/create" element={<CreateCard />} />
              <Route path="/view/:id" element={<ViewCard />} />
              <Route path="/admin" element={<Admin />} />
            </Routes>
          )}
        </main>
        
        <footer className="app-footer">
          <div className="container">
            <p>© 2025 {appName} - Send joy, one card at a time 💌</p>
          </div>
        </footer>
      </div>
    </Router>
  );
}

export default App
