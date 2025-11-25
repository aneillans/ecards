
import { useState, useEffect } from 'react';
import { useLocation, Navigate } from 'react-router-dom';
import { isLoggedIn, login } from '../auth';
import api from '../api';
import './Welcome.css';

function Welcome() {
  const [appName, setAppName] = useState('eCards');
  const location = useLocation();
  const loggedIn = isLoggedIn();

  console.log('Welcome component rendering, loggedIn:', loggedIn, 'pathname:', location.pathname);

  useEffect(() => {
    api.get('/ecards/config')
      .then(response => {
        if (response.data.appName) {
          setAppName(response.data.appName);
        }
      })
      .catch(err => console.error('Error fetching config:', err));
  }, []);

  // Only redirect if on root path and logged in
  if (loggedIn && location.pathname === '/') {
    return <Navigate to="/create" replace />;
  }

  const handleGetStarted = () => {
    login();
  };

  return (
    <div className="welcome-container">
      <div className="welcome-hero">
        <h1>🎉 Welcome to {appName}! 🎊</h1>
        <p className="hero-subtitle">
          Send joy, one card at a time.
        </p>
        <p className="hero-description">
          Create beautiful, personalized eCards and share them with the people you care about.
          Schedule them for special occasions or send them right away!
        </p>
        <button onClick={handleGetStarted} className="get-started-btn">
          🔐 Login to Get Started
        </button>
        <p className="login-note">
          You'll need to login to create and send cards
        </p>
      </div>
      <div className="features">
        <div className="feature">
          <span className="feature-icon">🎨</span>
          <h3>Custom or Premade Art</h3>
          <p>Choose from our templates or upload your own artwork</p>
        </div>
        <div className="feature">
          <span className="feature-icon">📅</span>
          <h3>Schedule Sending</h3>
          <p>Send now or schedule for a future date</p>
        </div>
        <div className="feature">
          <span className="feature-icon">📊</span>
          <h3>Track Views</h3>
          <p>See when your cards are opened and enjoyed</p>
        </div>
      </div>
    </div>
  );
}

export default Welcome;
