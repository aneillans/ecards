import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import api from '../api';

function ViewCard() {
  const { id } = useParams();
  const [card, setCard] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    const fetchCard = async () => {
      try {
        const response = await api.get(`/ecards/${id}`);
        setCard(response.data);

        // Record the view
        await api.get(`/ecards/${id}/view`);
      } catch (err) {
        setError(err.response?.data?.message || 'Failed to load eCard. It may have expired or been deleted.');
        console.error('Error fetching card:', err);
      } finally {
        setLoading(false);
      }
    };

    fetchCard();
  }, [id]);

  if (loading) {
    return <div className="loading">Loading your eCard... 🎨</div>;
  }

  if (error) {
    return (
      <div className="card-container">
        <div className="error-message">
          <h3>😢 Oops!</h3>
          <p>{error}</p>
        </div>
      </div>
    );
  }

  if (!card) {
    return (
      <div className="card-container">
        <div className="error-message">
          <h3>Card not found</h3>
          <p>This eCard doesn't exist or has been removed.</p>
        </div>
      </div>
    );
  }

  return (
    <div className="view-card">
      <h1>🎉 You've received an eCard! 🎉</h1>
      
      {card.customArtPath && (
        <div className="card-artwork">
          <img 
            src={`${import.meta.env.VITE_API_URL || 'http://localhost:5000/api'}/ecards/${id}/art`} 
            alt="Card artwork" 
            style={{ maxWidth: '100%', borderRadius: '8px', marginBottom: '1rem' }}
          />
        </div>
      )}
      
      {card.premadeArtId && !card.customArtPath && (
        <div className="card-artwork">
          <img 
            src={`${import.meta.env.VITE_API_URL || 'http://localhost:5000/api'}/templates/${card.premadeArtId}/image`} 
            alt="Card artwork" 
            style={{ maxWidth: '100%', borderRadius: '8px', marginBottom: '1rem' }}
          />
        </div>
      )}
      
      <div className="card-message">
        {card.message}
      </div>
      
      <div className="card-from">
        <p><strong>From:</strong> {card.sender?.name} ({card.sender?.email})</p>
        <p><strong>To:</strong> {card.recipientName}</p>
        {card.viewCount > 1 && (
          <p style={{marginTop: '1rem', fontSize: '0.9rem', color: '#999'}}>
            This card has been viewed {card.viewCount} times
          </p>
        )}
      </div>
    </div>
  );
}

export default ViewCard;
