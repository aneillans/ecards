import { useEffect, useState } from 'react';
import { hasRole } from '../auth';
import api from '../api';

export default function Admin(){
  const [ecards, setEcards] = useState([]);
  const [templates, setTemplates] = useState([]);
  const [error, setError] = useState(null);
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState({ id: '', name: '', category: '', iconEmoji: '', description: '', imagePath: '', isActive: true, sortOrder: 0 });

  async function loadAll(){
    try{
      const e = await api.get('/admin/ecards?take=50');
      const t = await api.get('/admin/templates');
      setEcards(e.data || []);
      setTemplates(t.data || []);
    }catch(err){ setError(err.response?.data?.message || err.message); }
  }

  useEffect(()=>{ let mounted = true; if (mounted) loadAll(); return ()=> mounted = false; }, []);

  function beginCreate(){
    setEditing('create');
    setForm({ id: '', name: '', category: '', iconEmoji: '', description: '', imagePath: '', isActive: true, sortOrder: 0 });
  }

  function beginEdit(t){
    setEditing('edit');
    setForm({ ...t });
  }

  async function submitForm(e){
    e?.preventDefault();
    try{
      if (editing === 'create'){
        const created = await api.post('/admin/templates', form);
        setTemplates([...(templates || []), created.data]);
      } else if (editing === 'edit'){
        await api.put(`/admin/templates/${form.id}`, form);
        setTemplates((templates||[]).map(t => t.id === form.id ? { ...t, ...form } : t));
      }
      setEditing(null);
    }catch(err){ setError(err.message); }
  }

  async function removeTemplate(id){
    if (!confirm('Delete template? This will soft-delete it.')) return;
    try{
      await api.delete(`/admin/templates/${id}`);
      setTemplates((templates||[]).filter(t => t.id !== id));
    }catch(err){ setError(err.response?.data?.message || err.message); }
  }

  async function resendEmail(cardId){
    if (!confirm('Resend email notification for this card?')) return;
    try{
      const response = await api.post(`/admin/ecards/${cardId}/resend`);
      alert(response.data.message || 'Email resent successfully');
      await loadAll(); // Reload to show updated sent date
    }catch(err){ 
      const msg = err.response?.data?.message || err.message;
      setError(msg);
      alert('Failed to resend email: ' + msg);
    }
  }

  async function deleteECard(cardId){
    if (!confirm('Delete this eCard permanently? This will remove it from the database and delete any custom media.')) return;
    try{
      const response = await api.delete(`/admin/ecards/${cardId}`);
      alert(response.data.message || 'eCard deleted successfully');
      await loadAll(); // Reload the list
    }catch(err){ 
      const msg = err.response?.data?.message || err.message;
      setError(msg);
      alert('Failed to delete eCard: ' + msg);
    }
  }

  // Preview
  const [preview, setPreview] = useState(null);
  function openPreview(t){ setPreview(t); }
  function closePreview(){ setPreview(null); }

  if (!hasRole('admin')) return <div className="container"><h2>Forbidden</h2><p>You must be an admin to view this page.</p></div>;

  return (
    <div className="container admin-page">
      <h2>Admin Dashboard</h2>
      {error && <div className="error">{error}</div>}

      <section>
        <h3>Recent eCards</h3>
        <table className="table">
          <thead><tr><th>Id</th><th>Recipient</th><th>Sent</th><th>Created</th><th>Views</th><th>Actions</th></tr></thead>
          <tbody>
            {(ecards||[]).map(c => (
              <tr key={c.id}>
                <td>{c.id}</td>
                <td>{c.recipientName} &lt;{c.recipientEmail}&gt;</td>
                <td>{c.isSent ? String(c.sentDate) : 'No'}</td>
                <td>{c.createdDate}</td>
                <td>{c.viewCount}</td>
                <td>
                  <a href={`/view/${c.id}`} target="_blank" rel="noopener noreferrer" style={{ textDecoration: 'underline', color: '#0066cc', marginRight: '8px' }}>
                    View Card
                  </a>
                  <button onClick={()=>resendEmail(c.id)} style={{ fontSize: '0.875rem', marginRight: '8px' }}>
                    Resend Email
                  </button>
                  <button onClick={()=>deleteECard(c.id)} style={{ fontSize: '0.875rem', backgroundColor: '#dc3545', color: 'white' }}>
                    Delete
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      <section>
        <h3>Templates</h3>
        <div style={{ marginBottom: '8px' }}>
          <button onClick={beginCreate}>Create Template</button>
        </div>

        <table className="table">
          <thead><tr><th>Id</th><th>Name</th><th>Category</th><th>Active</th><th>Actions</th></tr></thead>
          <tbody>
            {(templates||[]).map(t => (
              <tr key={t.id}><td>{t.id}</td><td>{t.name}</td><td>{t.category}</td><td>{String(t.isActive)}</td><td>
                <button onClick={()=>openPreview(t)}>Preview</button>
                <button onClick={()=>beginEdit(t)} style={{ marginLeft: '8px' }}>Edit</button>
                <button onClick={()=>removeTemplate(t.id)} style={{ marginLeft: '8px' }}>Delete</button>
              </td></tr>
            ))}
          </tbody>
        </table>
      </section>

      {editing && (
        <section className="template-form">
          <h3>{editing === 'create' ? 'Create' : 'Edit'} Template</h3>
          <form onSubmit={submitForm}>
            <div><label>Id (optional) <input value={form.id} onChange={e=>setForm({...form, id: e.target.value})} /></label></div>
            <div><label>Name <input required value={form.name} onChange={e=>setForm({...form, name: e.target.value})} /></label></div>
            <div><label>Category <input value={form.category} onChange={e=>setForm({...form, category: e.target.value})} /></label></div>
            <div><label>Icon <input value={form.iconEmoji} onChange={e=>setForm({...form, iconEmoji: e.target.value})} /></label></div>
            <div><label>Description <input value={form.description} onChange={e=>setForm({...form, description: e.target.value})} /></label></div>
            <div><label>Image Path <input value={form.imagePath} onChange={e=>setForm({...form, imagePath: e.target.value})} /></label></div>
            <div><label>Sort Order <input type="number" value={form.sortOrder} onChange={e=>setForm({...form, sortOrder: parseInt(e.target.value||'0')})} /></label></div>
            <div><label>Active <input type="checkbox" checked={form.isActive} onChange={e=>setForm({...form, isActive: e.target.checked})} /></label></div>
            <div style={{ marginTop: '8px' }}>
              <button type="submit">Save</button>
              <button type="button" onClick={()=>setEditing(null)} style={{ marginLeft: '8px' }}>Cancel</button>
            </div>
          </form>
        </section>
      )}

      {preview && (
        <div className="modal-overlay" style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.6)', display: 'flex', alignItems: 'center', justifyContent: 'center' }} onClick={closePreview}>
          <div className="modal" style={{ background: '#fff', padding: '16px', maxWidth: '640px', width: '90%', borderRadius: '6px' }} onClick={e=>e.stopPropagation()}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <h3>Template Preview - {preview.name}</h3>
              <button onClick={closePreview}>Close</button>
            </div>
            <div style={{ marginTop: '12px' }}>
              {preview.imagePath ? (
                <img src={preview.imagePath} alt={preview.name} style={{ maxWidth: '100%', borderRadius: '4px' }} />
              ) : (
                <div style={{ fontSize: '6rem', textAlign: 'center' }}>{preview.iconEmoji || '🎨'}</div>
              )}
              {preview.description && <p style={{ marginTop: '8px' }}>{preview.description}</p>}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
