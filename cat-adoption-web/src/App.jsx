import React, { useState, useEffect } from 'react'
import './App.css'

const API_URL = '/api'

export default function App() {
  const [view, setView] = useState('login') // login, register, cats, newCat
  const [token, setToken] = useState(localStorage.getItem('token'))
  const [user, setUser] = useState(localStorage.getItem('user') ? JSON.parse(localStorage.getItem('user')) : null)
  const [cats, setCats] = useState([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [formData, setFormData] = useState({})
  const [currentPage, setCurrentPage] = useState(1)
  const [totalPages, setTotalPages] = useState(1)
  const [imageFile, setImageFile] = useState(null)

  useEffect(() => {
    if (token && user) {
      setView('cats')
      loadCats()
    } else {
      setView('login')
    }
  }, [])

  // AUTH FUNCTIONS
  const handleLogin = async (e) => {
    e.preventDefault()
    setError('')
    try {
      const response = await fetch(`${API_URL}/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          email: formData.loginEmail,
          password: formData.loginPassword,
        }),
      })
      const data = await response.json()
      if (response.ok) {
        setToken(data.token)
        // Декодирай JWT токена за userId (или съхрани от регистрация)
        const userData = { email: formData.loginEmail, id: data.userId }
        setUser(userData)
        localStorage.setItem('token', data.token)
        localStorage.setItem('user', JSON.stringify(userData))
        setView('cats')
        setFormData({})
        loadCats()
      } else {
        setError(data.message || 'Login failed')
      }
    } catch (err) {
      setError('Connection error: ' + err.message)
    }
  }

  const handleRegister = async (e) => {
    e.preventDefault()
    setError('')
    try {
      const response = await fetch(`${API_URL}/auth/register`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          firstName: formData.firstName,
          lastName: formData.lastName,
          email: formData.registerEmail,
          password: formData.registerPassword,
        }),
      })
      const data = await response.json()
      if (response.ok) {
        // Auto login после регистрация
        const loginResponse = await fetch(`${API_URL}/auth/login`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            email: formData.registerEmail,
            password: formData.registerPassword,
          }),
        })
        const loginData = await loginResponse.json()
        if (loginResponse.ok) {
          setToken(loginData.token)
          const userData = { email: formData.registerEmail, id: data.userId }
          setUser(userData)
          localStorage.setItem('token', loginData.token)
          localStorage.setItem('user', JSON.stringify(userData))
          setFormData({})
          setView('cats')
          loadCats()
        } else {
          alert('Registration successful! Please login.')
          setView('login')
        }
      } else {
        setError(data.message || 'Registration failed')
      }
    } catch (err) {
      setError('Connection error: ' + err.message)
    }
  }

  // CATS FUNCTIONS
  const loadCats = async (page = 1) => {
    setLoading(true)
    try {
      const response = await fetch(`${API_URL}/cats?page=${page}&pageSize=10`)
      if (response.ok) {
        const data = await response.json()
        setCats(data.data)
        setTotalPages(data.totalPages)
        setCurrentPage(page)
      }
    } catch (err) {
      setError('Error loading cats: ' + err.message)
    }
    setLoading(false)
  }

  const handleNewCat = async (e) => {
    e.preventDefault()
    setError('')
    try {
      const formDataToSend = new FormData()
      formDataToSend.append('name', formData.catName)
      formDataToSend.append('age', parseInt(formData.catAge))
      formDataToSend.append('sex', formData.catSex)
      formDataToSend.append('color', formData.catColor)
      formDataToSend.append('description', formData.catDescription)
      formDataToSend.append('location', formData.catLocation)
      if (imageFile) {
        formDataToSend.append('image', imageFile)
      }

      const response = await fetch(`${API_URL}/cats`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
        },
        body: formDataToSend,
      })
      const data = await response.json()
      if (response.ok) {
        alert('Cat listing created successfully!')
        setView('cats')
        setFormData({})
        setImageFile(null)
        loadCats()
      } else {
        setError(data.message || 'Failed to create listing')
      }
    } catch (err) {
      setError('Connection error: ' + err.message)
    }
  }

  const handleDeleteCat = async (catId) => {
    if (!window.confirm('Are you sure?')) return
    try {
      const response = await fetch(`${API_URL}/cats/${catId}`, {
        method: 'DELETE',
        headers: { 'Authorization': `Bearer ${token}` },
      })
      if (response.ok) {
        loadCats()
      } else {
        setError('Failed to delete cat')
      }
    } catch (err) {
      setError('Error: ' + err.message)
    }
  }

  const handleLogout = () => {
    setToken(null)
    setUser(null)
    setCats([])
    localStorage.removeItem('token')
    localStorage.removeItem('user')
    setView('login')
    setFormData({})
  }

  const handleInputChange = (e) => {
    const { name, value } = e.target
    setFormData({ ...formData, [name]: value })
  }

  return (
    <div className="container">
      <header>
        <h1>🐱 Cat Adoption</h1>
        <nav>
          {token && user ? (
            <>
              <button className="btn-primary" onClick={() => setView('cats')}>🐱 Cats</button>
              <button className="btn-primary" onClick={() => setView('newCat')}>+ New Listing</button>
              <span style={{ color: '#666', padding: '10px' }}>Hi, {user.email}!</span>
              <button className="btn-logout" onClick={handleLogout}>Logout</button>
            </>
          ) : (
            <>
              <button className="btn-primary" onClick={() => setView('login')}>Login</button>
              <button className="btn-primary" onClick={() => setView('register')}>Register</button>
            </>
          )}
        </nav>
      </header>

      {error && <div className="alert error">{error}</div>}

      {/* LOGIN VIEW */}
      {view === 'login' && !token && (
        <div className="auth-container">
          <h2>Login</h2>
          <form onSubmit={handleLogin}>
            <div className="form-group">
              <label>Email</label>
              <input type="email" name="loginEmail" value={formData.loginEmail || ''} onChange={handleInputChange} required />
            </div>
            <div className="form-group">
              <label>Password</label>
              <input type="password" name="loginPassword" value={formData.loginPassword || ''} onChange={handleInputChange} required />
            </div>
            <input type="submit" value="Login" />
          </form>
          <div className="auth-link">
            Don't have an account? <a onClick={() => setView('register')}>Register</a>
          </div>
        </div>
      )}

      {/* REGISTER VIEW */}
      {view === 'register' && !token && (
        <div className="auth-container">
          <h2>Register</h2>
          <form onSubmit={handleRegister}>
            <div className="form-group">
              <label>First Name</label>
              <input type="text" name="firstName" value={formData.firstName || ''} onChange={handleInputChange} required />
            </div>
            <div className="form-group">
              <label>Last Name</label>
              <input type="text" name="lastName" value={formData.lastName || ''} onChange={handleInputChange} required />
            </div>
            <div className="form-group">
              <label>Email</label>
              <input type="email" name="registerEmail" value={formData.registerEmail || ''} onChange={handleInputChange} required />
            </div>
            <div className="form-group">
              <label>Password</label>
              <input type="password" name="registerPassword" value={formData.registerPassword || ''} onChange={handleInputChange} required />
            </div>
            <input type="submit" value="Register" />
          </form>
          <div className="auth-link">
            Already registered? <a onClick={() => setView('login')}>Login</a>
          </div>
        </div>
      )}

      {/* CATS LIST VIEW */}
      {view === 'cats' && token && (
        <div className="cats-section">
          <div className="section-header">
            <h2>🐱 Available Cats</h2>
            <button className="btn-primary" onClick={() => setView('newCat')}>+ New Listing</button>
          </div>
          {loading ? (
            <div className="loading">Loading...</div>
          ) : cats.length === 0 ? (
            <div className="empty-state">
              <h3>No cats yet</h3>
              <p>Create the first listing!</p>
            </div>
           ) : (
            <div>
              <div className="cats-grid">
                {cats.map((cat) => (
                  <div key={cat.id} className="cat-card">
                    <div className="cat-image-placeholder" style={{backgroundImage: cat.imageUrl ? `url(${cat.imageUrl})` : 'none', backgroundSize: 'cover', backgroundPosition: 'center'}}>
                      {!cat.imageUrl && '🐱'}
                    </div>
                    <div className="cat-content">
                      <h3>{cat.name}</h3>
                      <div className="cat-info">Age: {cat.age} years</div>
                      <div className="cat-info">Sex: {cat.sex}</div>
                      <div className="cat-info">Color: {cat.color}</div>
                      <div className="cat-info">Location: {cat.location}</div>
                      <div className="cat-info">Description: {cat.description}</div>
                      <div className="cat-owner">By: {cat.owner.firstName} {cat.owner.lastName}</div>
                      {user && user.id === cat.owner.id && (
                        <div className="cat-actions">
                          <button className="btn-delete" onClick={() => handleDeleteCat(cat.id)}>Delete</button>
                        </div>
                      )}
                    </div>
                  </div>
                ))}
              </div>
              <div style={{display: 'flex', justifyContent: 'center', gap: '10px', marginTop: '30px'}}>
                <button onClick={() => loadCats(currentPage - 1)} disabled={currentPage === 1} style={{padding: '10px 20px', background: currentPage === 1 ? '#ccc' : '#667eea', color: 'white', border: 'none', borderRadius: '5px', cursor: currentPage === 1 ? 'not-allowed' : 'pointer'}}>← Previous</button>
                <span style={{color: 'white', padding: '10px 20px'}}>Page {currentPage} of {totalPages}</span>
                <button onClick={() => loadCats(currentPage + 1)} disabled={currentPage === totalPages} style={{padding: '10px 20px', background: currentPage === totalPages ? '#ccc' : '#667eea', color: 'white', border: 'none', borderRadius: '5px', cursor: currentPage === totalPages ? 'not-allowed' : 'pointer'}}>Next →</button>
              </div>
            </div>
           )}
        </div>
      )}

      {/* NEW CAT VIEW */}
      {view === 'newCat' && token && (
        <div className="auth-container" style={{ maxWidth: '600px' }}>
          <h2>Create Cat Listing</h2>
          <form onSubmit={handleNewCat}>
            <div className="form-group">
              <label>Name</label>
              <input type="text" name="catName" value={formData.catName || ''} onChange={handleInputChange} required />
            </div>
            <div className="form-group">
              <label>Age</label>
              <input type="number" name="catAge" value={formData.catAge || ''} onChange={handleInputChange} required />
            </div>
            <div className="form-group">
              <label>Sex</label>
              <select name="catSex" value={formData.catSex || ''} onChange={handleInputChange} required>
                <option value="">Choose...</option>
                <option value="Male">Male</option>
                <option value="Female">Female</option>
              </select>
            </div>
            <div className="form-group">
              <label>Color</label>
              <input type="text" name="catColor" value={formData.catColor || ''} onChange={handleInputChange} required />
            </div>
            <div className="form-group">
              <label>Description</label>
              <textarea name="catDescription" value={formData.catDescription || ''} onChange={handleInputChange} rows="4" required />
            </div>
            <div className="form-group">
              <label>Location</label>
              <input type="text" name="catLocation" value={formData.catLocation || ''} onChange={handleInputChange} required />
            </div>
            <div className="form-group">
              <label>Cat Photo (optional)</label>
              <input type="file" accept="image/*" onChange={(e) => setImageFile(e.target.files?.[0] || null)} />
            </div>
            <input type="submit" value="Create Listing" />
          </form>
          <button style={{ marginTop: '10px', width: '100%', padding: '10px', background: '#999', color: 'white', border: 'none', borderRadius: '5px', cursor: 'pointer' }} onClick={() => setView('cats')}>Cancel</button>
        </div>
      )}
    </div>
  )
}

