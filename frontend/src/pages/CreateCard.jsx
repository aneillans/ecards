import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { isLoggedIn, login, tokenParsed } from '../auth';
import api from '../api';

function CreateCard() {
  const navigate = useNavigate();
  const [formData, setFormData] = useState({
    senderName: '',
    senderEmail: '',
    recipientName: '',
    recipientEmail: '',
    message: '',
    scheduledSendDate: '',
    sendNow: true,
    premadeArtId: ''
  });
  
  const [customArt, setCustomArt] = useState(null);
  const [previewUrl, setPreviewUrl] = useState(null);
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState(null);
  const [error, setError] = useState(null);
  const [uploadLimits, setUploadLimits] = useState({ maxUploadBytes: 5 * 1024 * 1024, maxImageDimension: 1600 });
  const [premadeTemplates, setPremadeTemplates] = useState([]);
  const [templatesLoading, setTemplatesLoading] = useState(true);
  const [myCards, setMyCards] = useState([]);
  const [userEmail, setUserEmail] = useState('');

  // Check authentication on mount
  useEffect(() => {
    if (!isLoggedIn()) {
      // Redirect to home page if not logged in
      navigate('/');
      return;
    }
    
    // Pre-fill sender info from token
    const parsed = tokenParsed();
    const email = parsed.email || parsed.preferred_username || '';
    setUserEmail(email);
    setFormData(prev => ({
      ...prev,
      senderEmail: email,
      senderName: parsed.name || parsed.given_name || ''
    }));

    // Fetch user's sent cards
    if (email) {
      fetchMyCards(email);
    }
    fetchLimits();
  }, [navigate]);

  const fetchLimits = async () => {
    try {
      const response = await api.get('/ecards/config');
      setUploadLimits({
        maxUploadBytes: response.data?.maxUploadBytes || 5 * 1024 * 1024,
        maxImageDimension: response.data?.maxImageDimension || 1600
      });
    } catch (err) {
      console.error('Error fetching upload limits:', err);
    }
  };

  const fetchMyCards = async (email) => {
    try {
      const response = await api.get(`/ecards/my-cards?email=${encodeURIComponent(email)}`);
      setMyCards(response.data);
    } catch (err) {
      console.error('Error fetching cards:', err);
    }
  };

  const handleResendEmail = async (cardId) => {
    if (!confirm('Resend email notification for this card?')) return;
    
    try {
      const response = await api.post(`/ecards/${cardId}/resend?senderEmail=${encodeURIComponent(userEmail)}`);
      alert(response.data.message || 'Email resent successfully');
      // Refresh cards list to show updated sent date
      fetchMyCards(userEmail);
    } catch (err) {
      const msg = err.response?.data?.message || err.message;
      alert('Failed to resend email: ' + msg);
    }
  };

  // Fetch templates from backend
  useEffect(() => {
    const fetchTemplates = async () => {
      try {
        const response = await api.get(`/templates`);
        setPremadeTemplates(response.data);
      } catch (err) {
        console.error('Error fetching templates:', err);
        setPremadeTemplates([]);
      } finally {
        setTemplatesLoading(false);
      }
    };

    fetchTemplates();
  }, []);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData(prev => ({ ...prev, [name]: value }));
  };

  const formatBytes = (bytes) => {
    if (!bytes) return '';
    const sizes = ['bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(1024));
    return `${(bytes / Math.pow(1024, i)).toFixed(1)} ${sizes[i]}`;
  };

  const handleFileChange = (e) => {
    const file = e.target.files[0];
    if (!file) {
      setCustomArt(null);
      setPreviewUrl(null);
      return;
    }

    const maxBytes = uploadLimits.maxUploadBytes || 5 * 1024 * 1024;
    if (file.size > maxBytes) {
      setError(`File is too large. Maximum allowed is ${formatBytes(maxBytes)}.`);
      setCustomArt(null);
      setPreviewUrl(null);
      e.target.value = '';
      return;
    }

    // Clear premade selection when uploading custom art
    setFormData(prev => ({ ...prev, premadeArtId: '' }));

    const reader = new FileReader();
    reader.onloadend = () => {
      const img = new Image();
      img.onload = () => {
        const maxDim = uploadLimits.maxImageDimension || 1600;
        if (maxDim && (img.width > maxDim || img.height > maxDim)) {
          setError(`Image dimensions are too large (${img.width}x${img.height}). Max allowed is ${maxDim}px on the longest side.`);
          setCustomArt(null);
          setPreviewUrl(null);
          e.target.value = '';
          return;
        }
        setError(null);
        setCustomArt(file);
        setPreviewUrl(reader.result);
      };
      img.onerror = () => {
        setError('Could not read the selected image.');
        setCustomArt(null);
        setPreviewUrl(null);
        e.target.value = '';
      };
      img.src = reader.result;
    };
    reader.readAsDataURL(file);
  };

  const handleTemplateSelect = (templateId) => {
    setFormData(prev => ({ ...prev, premadeArtId: templateId }));
    // Clear custom art when selecting a template
    setCustomArt(null);
    setPreviewUrl(null);
  };

  const handleSendTypeChange = (sendNow) => {
    setFormData(prev => ({ ...prev, sendNow }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setLoading(true);
    setError(null);
    setSuccess(null);

    try {
      const data = new FormData();
      data.append('senderName', formData.senderName);
      data.append('senderEmail', formData.senderEmail);
      data.append('recipientName', formData.recipientName);
      data.append('recipientEmail', formData.recipientEmail);
      data.append('message', formData.message);
      
      if (!formData.sendNow && formData.scheduledSendDate) {
        data.append('scheduledSendDate', formData.scheduledSendDate);
      }
      
      if (customArt) {
        data.append('customArt', customArt);
      }

      if (formData.premadeArtId) {
        data.append('premadeArtId', formData.premadeArtId);
      }

      const response = await api.post(`/ecards`, data, {
        headers: {
          'Content-Type': 'multipart/form-data'
        }
      });

      setSuccess(response.data);
      
      // Reset form
      const parsed = tokenParsed();
      setFormData({
        senderName: parsed.name || parsed.given_name || '',
        senderEmail: userEmail,
        recipientName: '',
        recipientEmail: '',
        message: '',
        scheduledSendDate: '',
        sendNow: true,
        premadeArtId: ''
      });
      setCustomArt(null);
      setPreviewUrl(null);

      // Refresh the cards list
      if (userEmail) {
        fetchMyCards(userEmail);
      }
      
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to create eCard. Please try again.');
      console.error('Error creating card:', err);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="card-container">
      {myCards.length > 0 && (
        <div className="my-cards-section" style={{marginBottom: '2rem'}}>
          <h2>Your Sent Cards</h2>
          <div className="cards-list">
            {myCards.map(card => (
              <div key={card.id} className="card-item">
                <div className="card-header">
                  <h3>To: {card.recipientName}</h3>
                  <span className="card-date">
                    {new Date(card.createdDate).toLocaleDateString()}
                  </span>
                </div>
                <div className="card-details">
                  <p className="card-message">{card.message.substring(0, 100)}{card.message.length > 100 ? '...' : ''}</p>
                  <div className="card-stats">
                    <span className={`status ${card.viewCount > 0 ? 'opened' : 'unopened'}`}>
                      {card.viewCount > 0 ? `✓ Opened ${card.viewCount} time${card.viewCount > 1 ? 's' : ''}` : '○ Not opened yet'}
                    </span>
                    {card.firstViewedDate && (
                      <span className="viewed-date">
                        First viewed: {new Date(card.firstViewedDate).toLocaleDateString()}
                      </span>
                    )}
                    {!card.isSent && card.scheduledSendDate && (
                      <span className="scheduled">
                        Scheduled for: {new Date(card.scheduledSendDate).toLocaleString()}
                      </span>
                    )}
                  </div>
                </div>
                <div className="card-actions">
                  <a 
                    href={`/view/${card.id}`} 
                    target="_blank" 
                    rel="noopener noreferrer"
                    className="view-link"
                  >
                    View Card
                  </a>
                  <button 
                    onClick={() => handleResendEmail(card.id)}
                    className="resend-button"
                    type="button"
                    style={{ marginLeft: '8px', padding: '6px 12px', fontSize: '0.875rem' }}
                  >
                    Resend Email
                  </button>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      <h2>Create Your eCard</h2>
      
      {success && (
        <div className="success-message">
          <h3>eCard Created Successfully!</h3>
          <p>
            <a href={`/view/${success.id}`} target="_blank" rel="noopener noreferrer">View your card here</a>
          </p>
          <p style={{marginTop: '1rem', fontSize: '0.9rem'}}>
            {success.scheduledSendDate && new Date(success.scheduledSendDate) > new Date() ? 
              `Your card will be sent on ${new Date(success.scheduledSendDate).toLocaleString()}` :
              'Your card will be sent soon!'
            }
          </p>
        </div>
      )}
      
      {error && (
        <div className="error-message">
          {error}
        </div>
      )}

      <form onSubmit={handleSubmit}>
        <div className="form-group">
          <label>Your Name *</label>
          <input
            type="text"
            name="senderName"
            value={formData.senderName}
            onChange={handleChange}
            required
            placeholder="Enter your name"
          />
        </div>

        <div className="form-group">
          <label>Your Email *</label>
          <input
            type="email"
            name="senderEmail"
            value={formData.senderEmail}
            onChange={handleChange}
            required
            placeholder="your@email.com"
          />
        </div>

        <div className="form-group">
          <label>Recipient Name *</label>
          <input
            type="text"
            name="recipientName"
            value={formData.recipientName}
            onChange={handleChange}
            required
            placeholder="Enter recipient's name"
          />
        </div>

        <div className="form-group">
          <label>Recipient Email *</label>
          <input
            type="email"
            name="recipientEmail"
            value={formData.recipientEmail}
            onChange={handleChange}
            required
            placeholder="recipient@email.com"
          />
        </div>

        <div className="form-group">
          <label>Your Message *</label>
          <textarea
            name="message"
            value={formData.message}
            onChange={handleChange}
            required
            placeholder="Write your heartfelt message here..."
          />
        </div>

        <div className="form-group">
          <label>Choose a Template</label>
          <p className="help-text">Select a premade template or upload your own artwork below</p>
          {templatesLoading ? (
            <div className="templates-loading">Loading templates...</div>
          ) : premadeTemplates.length > 0 ? (
            <div className="template-grid">
              {premadeTemplates.map(template => (
                <div
                  key={template.id}
                  className={`templateCard ${formData.premadeArtId === template.id ? 'selected' : ''}`}
                  onClick={() => handleTemplateSelect(template.id)}
                  title={template.description}
                >
                  <div className="template-emoji">{template.iconEmoji}</div>
                  <div className="template-name">{template.name}</div>
                  {template.category && (
                    <div className="template-category">{template.category}</div>
                  )}
                </div>
              ))}
            </div>
          ) : (
            <div className="templates-empty">No templates available</div>
          )}
        </div>

        <div className="form-group">
          <label>Or Upload Custom Artwork</label>
          <p className="help-text">
            Max size {formatBytes(uploadLimits.maxUploadBytes)}; images larger than {uploadLimits.maxImageDimension}px on the longest side are not allowed.
          </p>
          <div className="file-upload" onClick={() => document.getElementById('fileInput').click()}>
            <input
              id="fileInput"
              type="file"
              accept="image/*"
              onChange={handleFileChange}
            />
            <span className="file-upload-label">
              {customArt ? `✓ ${customArt.name}` : '📁 Click to upload image'}
            </span>
          </div>
          {previewUrl && (
            <img src={previewUrl} alt="Preview" className="preview-image" />
          )}
        </div>

        <div className="form-group">
          <label>When to send?</label>
          <div className="radio-group">
            <div className="radio-option">
              <input
                type="radio"
                id="sendNow"
                checked={formData.sendNow}
                onChange={() => handleSendTypeChange(true)}
              />
              <label htmlFor="sendNow" style={{marginBottom: 0}}>Send Now</label>
            </div>
            <div className="radio-option">
              <input
                type="radio"
                id="schedule"
                checked={!formData.sendNow}
                onChange={() => handleSendTypeChange(false)}
              />
              <label htmlFor="schedule" style={{marginBottom: 0}}>Schedule for Later</label>
            </div>
          </div>
        </div>

        {!formData.sendNow && (
          <div className="form-group">
            <label>Scheduled Date & Time</label>
            <input
              type="datetime-local"
              name="scheduledSendDate"
              value={formData.scheduledSendDate}
              onChange={handleChange}
              required={!formData.sendNow}
            />
            <p className="help-text">
              Choose when you want the card to be available for viewing
            </p>
          </div>
        )}

        <button type="submit" disabled={loading}>
          {loading ? '✨ Creating...' : '🎉 Create eCard'}
        </button>
      </form>
    </div>
  );
}

export default CreateCard;
