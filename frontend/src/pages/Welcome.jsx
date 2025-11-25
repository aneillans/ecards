
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
          Create beautiful, personalised eCards and share them with the people you care about.
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

      {/* FAQ Section */}
      <div className="faq">
        <h2 className="faq-title">Frequently Asked Questions</h2>
        <FAQ />
      </div>
    </div>
  );
}

// Simple Accordion-style FAQ
function FAQ() {
  const [openIndex, setOpenIndex] = useState(null);
  const faqs = [
    {
      q: 'Do I need an account to send a card?',
      a: 'Yes. In order to control spam, we require users to authenticate before sending cards. You can login via GMail and Discord at present, as well as a basic username and password. All access is via a secure OAuth2 server, and all data is encrypted for your protection.'
    },
    {
      q: 'Can I schedule a card for later?',
      a: 'Absolutely. When creating a card, choose a future date to have it sent automatically.'
    },
    {
      q: 'Can I upload my own artwork?',
      a: 'Yes. You can upload custom art and we hope to have templates available to select from soon!'
    },
    {
      q: 'How do recipients view the card?',
      a: 'They receive a link via email. You can also see when it is viewed!'
    },
    {
      q: 'How long is my eCard and art retained for?',
      a: 'Unviewed eCards and associated artwork are retained for up to 30 days (immediate sent cards are only stored for 14 days). Once viewed, the eCard and artwork are permanently deleted from our servers after 14 days.'
    },
    {
      q: 'I need to get in contact with someone about this service. How can I do that?',
      a: `Drop an email to ${window.ENV?.SUPPORT_EMAIL || 'support@example.com'}`
    }    
  ];

  const toggle = (idx) => {
    setOpenIndex((prev) => (prev === idx ? null : idx));
  };

  return (
    <div className="faq-list">
      {faqs.map((item, idx) => (
        <div key={idx} className={`faq-item ${openIndex === idx ? 'open' : ''}`}>
          <button className="faq-question" onClick={() => toggle(idx)} aria-expanded={openIndex === idx}>
            <span>{item.q}</span>
            <span className="faq-toggle">{openIndex === idx ? '−' : '+'}</span>
          </button>
          {openIndex === idx && (
            <div className="faq-answer">
              <p>{item.a}</p>
            </div>
          )}
        </div>
      ))}
    </div>
  );
}

export default Welcome;
