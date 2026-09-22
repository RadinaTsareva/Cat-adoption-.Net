import React, { useState, useEffect } from 'react'
import './App.css'

// Detect API URL at runtime
const getApiUrl = () => {
  // If build-time env var was set, use it
  if (typeof __VITE_API_BACKEND__ !== 'undefined' && __VITE_API_BACKEND__) {
    console.log('Using build-time API URL:', __VITE_API_BACKEND__)
    return __VITE_API_BACKEND__
  }
  
  // Auto-detect: if on Vercel production domain, use Railway backend
  if (typeof window !== 'undefined' && window.location.hostname.includes('vercel.app')) {
    const railwayUrl = 'https://cat-adoption-net-production.up.railway.app'
    console.log('Detected Vercel domain, using Railway backend:', railwayUrl)
    return railwayUrl
  }
  
  // Otherwise use local proxy (works for Docker and dev)
  console.log('Using local API proxy: /api')
  return '/api'
}

const API_URL = getApiUrl()

const CAT_STATUSES = {
  waitingAdoption: 'waiting-adoption',
  inProgress: 'in-progress',
  adopted: 'adopted',
}

const getCatFiltersFromUrl = () => {
  if (typeof window === 'undefined') {
    return { sex: '', color: '', status: '', city: '' }
  }

  const params = new URLSearchParams(window.location.search)
  return {
    sex: params.get('sex') || '',
    color: params.get('color') || '',
    status: params.get('status') || '',
    city: params.get('city') || '',
  }
}

const getCatPageFromUrl = () => {
  if (typeof window === 'undefined') {
    return 1
  }

  const params = new URLSearchParams(window.location.search)
  const page = Number.parseInt(params.get('page') || '1', 10)
  return Number.isFinite(page) && page > 0 ? page : 1
}

const updateCatUrl = (page, filters) => {
  if (typeof window === 'undefined') {
    return
  }

  const params = new URLSearchParams()
  const nextFilters = filters || {}

  Object.entries(nextFilters).forEach(([key, value]) => {
    if (value && value.trim()) {
      params.set(key, value.trim())
    }
  })

  if (page && page > 1) {
    params.set('page', String(page))
  }

  const query = params.toString()
  const nextUrl = `${window.location.pathname}${query ? `?${query}` : ''}`
  window.history.replaceState({}, '', nextUrl)
}

const normalizeCatStatus = (status) => {
  const normalized = (status || '').toString().trim().toLowerCase()

  switch (normalized) {
    case CAT_STATUSES.adopted:
      return CAT_STATUSES.adopted
    case CAT_STATUSES.inProgress:
    case 'in progress':
    case 'in process of adoption':
      return CAT_STATUSES.inProgress
    case CAT_STATUSES.waitingAdoption:
    case 'waiting adoption':
    case 'available':
    default:
      return CAT_STATUSES.waitingAdoption
  }
}

const getCatStatusLabel = (status) => {
  switch (normalizeCatStatus(status)) {
    case CAT_STATUSES.inProgress:
      return 'In process of adoption'
    case CAT_STATUSES.adopted:
      return 'Adopted'
    default:
      return 'Waiting adoption'
  }
}

const canUsersChat = (roleA, roleB) => (
  (roleA === 'care-giver' && roleB === 'pet-adopter') ||
  (roleA === 'pet-adopter' && roleB === 'care-giver')
)

export default function App() {
  const [view, setView] = useState('cats') // login, register, cats, newCat, admin, chat
  const [token, setToken] = useState(localStorage.getItem('token'))
  const [user, setUser] = useState(localStorage.getItem('user') ? JSON.parse(localStorage.getItem('user')) : null)
  const [cats, setCats] = useState([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [formData, setFormData] = useState({})
  const [currentPage, setCurrentPage] = useState(getCatPageFromUrl())
  const [totalPages, setTotalPages] = useState(1)
  const [imageFile, setImageFile] = useState(null)
  const [editingCatId, setEditingCatId] = useState(null)
  const [catFilters, setCatFilters] = useState(getCatFiltersFromUrl())
  const [adminUsers, setAdminUsers] = useState([])
  const [adminLoading, setAdminLoading] = useState(false)
  const [adminError, setAdminError] = useState('')
  const [chatConversations, setChatConversations] = useState([])
  const [chatContacts, setChatContacts] = useState([])
  const [chatMessages, setChatMessages] = useState([])
  const [chatLoading, setChatLoading] = useState(false)
  const [chatSending, setChatSending] = useState(false)
  const [chatError, setChatError] = useState('')
  const [chatTab, setChatTab] = useState('conversations')
  const [chatDraft, setChatDraft] = useState('')
  const [activeChat, setActiveChat] = useState({ conversationId: null, receiver: null })
  const isAdmin = user?.role === 'admin'
  const canCreateListings = isAdmin || user?.role === 'care-giver'
  const canUseChat = user?.role === 'care-giver' || user?.role === 'pet-adopter'
  const unreadChatCount = chatConversations.reduce((total, conversation) => total + (conversation.unreadCount || 0), 0)
  const hasCatFilters = Object.values(catFilters).some((value) => value && value.trim())

  useEffect(() => {
    const bootstrapAuth = async () => {
      setView('cats')
      loadCats(getCatPageFromUrl(), getCatFiltersFromUrl())

      if (!token) {
        setUser(null)
        return
      }

      const currentUser = await loadCurrentUser(token)
      if (!currentUser) {
        handleLogout()
        return
      }

      if (currentUser.role === 'care-giver' || currentUser.role === 'pet-adopter') {
        await loadChatData()
      }
    }

    bootstrapAuth()
  }, [])

  useEffect(() => {
    if (view === 'admin' && token && isAdmin) {
      loadAdminUsers()
    }
  }, [view, token, isAdmin])

  useEffect(() => {
    if (view === 'chat' && token && canUseChat) {
      loadChatData()
    }
  }, [view, token, canUseChat])

  useEffect(() => {
    if (!token || !canUseChat) {
      return undefined
    }

    const refreshChatSummaries = async () => {
      await loadChatData()
    }

    refreshChatSummaries()

    const intervalId = window.setInterval(refreshChatSummaries, 30000)
    const handleFocus = () => refreshChatSummaries()

    window.addEventListener('focus', handleFocus)

    return () => {
      window.clearInterval(intervalId)
      window.removeEventListener('focus', handleFocus)
    }
  }, [token, canUseChat])

  // AUTH FUNCTIONS
  const loadCurrentUser = async (accessToken = token) => {
    if (!accessToken) return null

    try {
      const response = await fetch(`${API_URL}/users/me`, {
        headers: {
          Authorization: `Bearer ${accessToken}`,
        },
      })

      if (!response.ok) {
        return null
      }

      const data = await response.json()
      const userData = {
        id: data.id,
        email: data.email,
        firstName: data.firstName,
        lastName: data.lastName,
        role: data.role,
        city: data.city,
      }

      setUser(userData)
      localStorage.setItem('user', JSON.stringify(userData))
      return userData
    } catch (err) {
      setError('Error loading profile: ' + err.message)
      return null
    }
  }

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
        localStorage.setItem('token', data.token)
        const currentUser = await loadCurrentUser(data.token)
        if (currentUser) {
          setView('cats')
          setFormData({})
          if (currentUser.role === 'care-giver' || currentUser.role === 'pet-adopter') {
            await loadChatData()
          }
          loadCats()
        } else {
          handleLogout()
        }
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
          role: formData.registerRole || 'pet-adopter',
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
          localStorage.setItem('token', loginData.token)
          const currentUser = await loadCurrentUser(loginData.token)
          if (!currentUser) {
            handleLogout()
            return
          }
          setFormData({})
          setView('cats')
          if (currentUser.role === 'care-giver' || currentUser.role === 'pet-adopter') {
            await loadChatData()
          }
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
  const loadCats = async (page = 1, filters = catFilters) => {
    setLoading(true)
    setError('')
    try {
      const queryParams = new URLSearchParams({
        page: String(page),
        pageSize: '10',
      })

      Object.entries(filters || {}).forEach(([key, value]) => {
        if (value && value.trim()) {
          queryParams.set(key, value.trim())
        }
      })

      const response = await fetch(`${API_URL}/cats?${queryParams.toString()}`)
      if (response.ok) {
        const data = await response.json()
        setCats(data.data || [])
        setTotalPages(data.totalPages || 1)
        setCurrentPage(page)
        updateCatUrl(page, filters)
      } else {
        setError('Failed to load cats')
        setCats([])
      }
    } catch (err) {
      setError('Error loading cats: ' + err.message)
      setCats([])
    }
    setLoading(false)
  }

  const resetCatForm = () => {
    setFormData({})
    setImageFile(null)
    setEditingCatId(null)
  }

  const handleStartEditCat = (cat) => {
    setFormData({
      catName: cat.name,
      catAge: cat.age,
      catSex: cat.sex,
      catColor: cat.color,
      catDescription: cat.description,
      catLocation: cat.location,
      catStatus: normalizeCatStatus(cat.status),
    })
    setImageFile(null)
    setEditingCatId(cat.id)
    setView('newCat')
  }

  const handleCancelCatForm = () => {
    resetCatForm()
    setView('cats')
  }

  const handleCatFilterChange = (e) => {
    const { name, value } = e.target
    setCatFilters((prev) => ({ ...prev, [name]: value }))
  }

  const handleApplyCatFilters = (e) => {
    e.preventDefault()
    loadCats(1, catFilters)
  }

  const handleResetCatFilters = () => {
    const resetFilters = {
      sex: '',
      color: '',
      status: '',
      city: '',
    }

    setCatFilters(resetFilters)
    loadCats(1, resetFilters)
  }

  const loadAdminUsers = async () => {
    if (!token || !isAdmin) return

    setAdminLoading(true)
    setAdminError('')
    try {
      const response = await fetch(`${API_URL}/admin/users`, {
        headers: { 'Authorization': `Bearer ${token}` },
      })

      if (!response.ok) {
        const data = await response.json().catch(() => ({}))
        setAdminError(data.message || 'Failed to load users')
        setAdminUsers([])
        return
      }

      const data = await response.json()
      setAdminUsers(data || [])
    } catch (err) {
      setAdminError('Error loading users: ' + err.message)
      setAdminUsers([])
    } finally {
      setAdminLoading(false)
    }
  }

  const loadChatData = async () => {
    if (!token || !canUseChat) return

    setChatLoading(true)
    setChatError('')

    try {
      const [conversationsResponse, contactsResponse] = await Promise.all([
        fetch(`${API_URL}/messages/conversations`, {
          headers: { Authorization: `Bearer ${token}` },
        }),
        fetch(`${API_URL}/messages/contacts`, {
          headers: { Authorization: `Bearer ${token}` },
        }),
      ])

      if (!conversationsResponse.ok || !contactsResponse.ok) {
        const conversationsError = await conversationsResponse.json().catch(() => ({}))
        const contactsError = await contactsResponse.json().catch(() => ({}))
        setChatError(conversationsError.message || contactsError.message || 'Failed to load chat data')
        setChatConversations([])
        setChatContacts([])
        return { conversations: [], contacts: [] }
      }

      const conversationsData = await conversationsResponse.json()
      const contactsData = await contactsResponse.json()

      setChatConversations(conversationsData || [])
      setChatContacts(contactsData || [])
      return { conversations: conversationsData || [], contacts: contactsData || [] }
    } catch (err) {
      setChatError('Error loading chat data: ' + err.message)
      setChatConversations([])
      setChatContacts([])
      return { conversations: [], contacts: [] }
    } finally {
      setChatLoading(false)
    }
  }

  const loadChatConversation = async (conversationId) => {
    if (!token || !conversationId) return

    setChatLoading(true)
    setChatError('')

    try {
      const response = await fetch(`${API_URL}/messages/conversations/${conversationId}`, {
        headers: { Authorization: `Bearer ${token}` },
      })

      const data = await response.json().catch(() => ({}))
      if (!response.ok) {
        setChatError(data.message || 'Failed to load conversation')
        setChatMessages([])
        return
      }

      setActiveChat({ conversationId: data.conversationId, receiver: data.otherUser || null })
      setChatMessages(data.messages || [])
      await loadChatData()
    } catch (err) {
      setChatError('Error loading conversation: ' + err.message)
      setChatMessages([])
    } finally {
      setChatLoading(false)
    }
  }

  const openConversation = async (conversation) => {
    if (!conversation) return

    setChatTab('conversations')
    setChatDraft('')
    await loadChatConversation(conversation.conversationId)
  }

  const openContactChat = async (contact) => {
    if (!contact) return

    setChatTab('contacts')
    setChatDraft('')

    let conversationsToSearch = chatConversations
    if (chatConversations.length === 0) {
      const loadedData = await loadChatData()
      conversationsToSearch = loadedData?.conversations || []
    }

    const existingConversation = conversationsToSearch.find(
      (conversation) => conversation.otherUserId === contact.id
    )

    if (existingConversation) {
      await loadChatConversation(existingConversation.conversationId)
      return
    }

    setActiveChat({ conversationId: null, receiver: contact })
    setChatMessages([])
  }

  const handleSendChatMessage = async (e) => {
    e.preventDefault()

    if (!activeChat.receiver?.id || !chatDraft.trim()) {
      return
    }

    setChatSending(true)
    setChatError('')

    try {
      const response = await fetch(`${API_URL}/messages`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({
          receiverId: activeChat.receiver.id,
          content: chatDraft,
        }),
      })

      const data = await response.json().catch(() => ({}))
      if (!response.ok) {
        setChatError(data.message || 'Failed to send message')
        return
      }

      setChatDraft('')
      await loadChatConversation(data.conversationId)
    } catch (err) {
      setChatError('Error sending message: ' + err.message)
    } finally {
      setChatSending(false)
    }
  }

  const handleUpdateUserRole = async (userId, role) => {
    setAdminError('')
    try {
      const response = await fetch(`${API_URL}/admin/users/${userId}/role`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`,
        },
        body: JSON.stringify({ role }),
      })

      const data = await response.json().catch(() => ({}))
      if (response.ok) {
        await loadAdminUsers()
      } else {
        setAdminError(data.message || 'Failed to update role')
      }
    } catch (err) {
      setAdminError('Error: ' + err.message)
    }
  }

  const handleDeleteUser = async (userId) => {
    if (!window.confirm('Delete this user? This will also remove their cats.')) return

    setAdminError('')
    try {
      const response = await fetch(`${API_URL}/admin/users/${userId}`, {
        method: 'DELETE',
        headers: { 'Authorization': `Bearer ${token}` },
      })

      const data = await response.json().catch(() => ({}))
      if (response.ok) {
        await loadAdminUsers()
        loadCats(currentPage)
      } else {
        setAdminError(data.message || 'Failed to delete user')
      }
    } catch (err) {
      setAdminError('Error: ' + err.message)
    }
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
      formDataToSend.append('status', formData.catStatus || CAT_STATUSES.waitingAdoption)
      if (imageFile) {
        formDataToSend.append('image', imageFile)
      }

      const url = editingCatId ? `${API_URL}/cats/${editingCatId}` : `${API_URL}/cats`
      const method = editingCatId ? 'PUT' : 'POST'
      const response = await fetch(url, {
        method,
        headers: {
          'Authorization': `Bearer ${token}`,
        },
        body: formDataToSend,
      })
      const data = await response.json()
      if (response.ok) {
        alert(editingCatId ? 'Cat listing updated successfully!' : 'Cat listing created successfully!')
        resetCatForm()
        setView('cats')
        loadCats(currentPage)
      } else {
        setError(data.message || (editingCatId ? 'Failed to update listing' : 'Failed to create listing'))
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
        const data = await response.json().catch(() => ({}))
        setError(data.message || 'Failed to delete cat')
      }
    } catch (err) {
      setError('Error: ' + err.message)
    }
  }

  const handleSeedTenCats = async () => {
    setError('')
    try {
      const response = await fetch(`${API_URL}/admin/seed-cats`, {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${token}` },
      })

      const data = await response.json().catch(() => ({}))
      if (response.ok) {
        alert(data.message || '10 cats were created successfully.')
        setView('cats')
        loadCats(1)
      } else {
        setError(data.message || 'Failed to create cats')
      }
    } catch (err) {
      setError('Error: ' + err.message)
    }
  }

  const handleLogout = () => {
    setToken(null)
    setUser(null)
    setCats([])
    setChatConversations([])
    setChatContacts([])
    setChatMessages([])
    setChatError('')
    setChatDraft('')
    setActiveChat({ conversationId: null, receiver: null })
    localStorage.removeItem('token')
    localStorage.removeItem('user')
    setView('cats')
    resetCatForm()
    loadCats(1)
  }

  const handleInputChange = (e) => {
    const { name, value } = e.target
    setFormData({ ...formData, [name]: value })
  }

  return (
    <div className="container">
      <header>
        <div className="header-top">
          <h1>🐱 Cat Adoption</h1>
          <nav>
            <>
              <button className="btn-primary" onClick={() => setView('cats')}>🐱 Cats</button>
              {token && canUseChat && (
                <button className="btn-primary" onClick={() => setView('chat')} style={{ position: 'relative' }}>
                  💬 Chat
                  {unreadChatCount > 0 && (
                    <span
                      style={{
                        marginLeft: '8px',
                        display: 'inline-flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        minWidth: '22px',
                        height: '22px',
                        padding: '0 6px',
                        borderRadius: '999px',
                        background: '#e74c3c',
                        color: 'white',
                        fontSize: '12px',
                        fontWeight: '700',
                        lineHeight: '1'
                      }}
                    >
                      {unreadChatCount > 99 ? '99+' : unreadChatCount}
                    </span>
                  )}
                </button>
              )}
              {token && user ? (
                <>
                  {canCreateListings && <button className="btn-primary" onClick={() => { resetCatForm(); setView('newCat') }}>+ New Listing</button>}
                  {isAdmin && <button className="btn-primary" onClick={() => setView('admin')}>🛠 Admin Panel</button>}
                  <span style={{ color: '#666', padding: '10px' }}>
                    Hi, {user.firstName || user.email}!
                    <span className="role-badge">{user.role}</span>
                  </span>
                  <button className="btn-logout" onClick={handleLogout}>Logout</button>
                </>
              ) : (
                <>
                  <button className="btn-primary" onClick={() => setView('login')}>Login</button>
                  <button className="btn-primary" onClick={() => setView('register')}>Register</button>
                </>
              )}
            </>
          </nav>
        </div>

        <form className="cat-filters header-filters" onSubmit={handleApplyCatFilters}>
          <div className="cat-filters__grid">
            <div className="form-group">
              <label className="sr-only">Sex</label>
              <select name="sex" value={catFilters.sex} onChange={handleCatFilterChange} aria-label="Sex">
                <option value="">Any sex</option>
                <option value="Male">Male</option>
                <option value="Female">Female</option>
              </select>
            </div>
            <div className="form-group">
              <label className="sr-only">Color</label>
              <input type="text" name="color" value={catFilters.color} onChange={handleCatFilterChange} placeholder="Color" aria-label="Color" />
            </div>
            <div className="form-group">
              <label className="sr-only">Status</label>
              <select name="status" value={catFilters.status} onChange={handleCatFilterChange} aria-label="Status">
                <option value="">Any status</option>
                <option value={CAT_STATUSES.waitingAdoption}>Waiting adoption</option>
                <option value={CAT_STATUSES.inProgress}>In process of adoption</option>
                <option value={CAT_STATUSES.adopted}>Adopted</option>
              </select>
            </div>
            <div className="form-group">
              <label className="sr-only">City</label>
              <input type="text" name="city" value={catFilters.city} onChange={handleCatFilterChange} placeholder="City" aria-label="City" />
            </div>
          </div>
          <div className="cat-filters__actions">
            <button type="submit" className="btn-primary">Search</button>
            <button type="button" className="btn-secondary" onClick={handleResetCatFilters}>Reset</button>
          </div>
        </form>
      </header>

      {error && <div className="alert error">{error}</div>}

      {/* LOGIN VIEW */}
      {view === 'login' && !token && (
        <div className="auth-container">
          <h2>Login</h2>
          <p className="admin-description" style={{ marginBottom: '15px' }}>
            Demo admin: <strong>john@example.com</strong> / <strong>password123</strong>
          </p>
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
            <div className="form-group">
              <label>Role</label>
              <select name="registerRole" value={formData.registerRole || 'pet-adopter'} onChange={handleInputChange} required>
                <option value="pet-adopter">pet-adopter</option>
                <option value="care-giver">care-giver</option>
              </select>
            </div>
            <input type="submit" value="Register" />
          </form>
          <div className="auth-link">
            Already registered? <a onClick={() => setView('login')}>Login</a>
          </div>
        </div>
      )}

      {/* CATS LIST VIEW */}
      {view === 'cats' && (
        <div className="cats-section">
          {loading ? (
            <div className="loading">Loading...</div>
          ) : cats.length === 0 ? (
            <div className="empty-state">
              <h3>No cats yet</h3>
              <p>{hasCatFilters ? 'No cats match the selected filters.' : 'Create the first listing!'}</p>
            </div>
           ) : (
            <div>
              <div className="cats-grid">
                {cats.map((cat) => (
                  <div key={cat.id} className={`cat-card ${normalizeCatStatus(cat.status) === CAT_STATUSES.adopted ? 'cat-card--adopted' : ''}`}>
                    <div className="cat-image-placeholder" style={{backgroundImage: cat.imageUrl ? `url(${cat.imageUrl})` : 'none', backgroundSize: 'cover', backgroundPosition: 'center'}}>
                      {!cat.imageUrl && '🐱'}
                    </div>
                    <div className="cat-content">
                      <h3>{cat.name}</h3>
                      <div className="cat-status-badge">{getCatStatusLabel(cat.status)}</div>
                      <div className="cat-info">Age: {cat.age} years</div>
                      <div className="cat-info">Sex: {cat.sex}</div>
                      <div className="cat-info">Color: {cat.color}</div>
                      <div className="cat-info">Location: {cat.location}</div>
                      <div className="cat-info">Description: {cat.description}</div>
                      <div className="cat-owner">By: {cat.owner?.firstName} {cat.owner?.lastName}</div>
                      {token && user && cat.owner?.id && (user.role === 'admin' || user.id === cat.owner.id) && (
                        <div className="cat-actions">
                          <button className="btn-edit" onClick={() => handleStartEditCat(cat)}>Edit</button>
                          <button className="btn-delete" onClick={() => handleDeleteCat(cat.id)}>Delete</button>
                        </div>
                      )}
                      {token && user && cat.owner?.id && user.id !== cat.owner.id && canUsersChat(user.role, cat.owner.role) && (
                        <div className="cat-actions">
                          <button className="btn-primary" onClick={() => { setView('chat'); openContactChat(cat.owner) }}>💬 Message owner</button>
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
          <h2>{editingCatId ? 'Edit Cat Listing' : 'Create Cat Listing'}</h2>
          {!canCreateListings && (
            <div className="alert error" style={{ marginBottom: '20px' }}>
              Only admins and care-givers can create listings.
            </div>
          )}
          {canCreateListings ? (
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
              <label>Status</label>
              <select name="catStatus" value={formData.catStatus || CAT_STATUSES.waitingAdoption} onChange={handleInputChange} required>
                <option value={CAT_STATUSES.waitingAdoption}>Waiting adoption</option>
                <option value={CAT_STATUSES.inProgress}>In process of adoption</option>
                <option value={CAT_STATUSES.adopted}>Adopted</option>
              </select>
            </div>
            <div className="form-group">
              <label>Cat Photo (optional)</label>
              {editingCatId && <div style={{ marginBottom: '8px', color: '#666', fontSize: '13px' }}>Choose a new photo to replace the current one.</div>}
              <input type="file" accept="image/*" onChange={(e) => setImageFile(e.target.files?.[0] || null)} />
            </div>
            <input type="submit" value={editingCatId ? 'Update Listing' : 'Create Listing'} />
          </form>
          ) : null}
          <button style={{ marginTop: '10px', width: '100%', padding: '10px', background: '#999', color: 'white', border: 'none', borderRadius: '5px', cursor: 'pointer' }} onClick={handleCancelCatForm}>Cancel</button>
        </div>
      )}

      {/* ADMIN VIEW */}
      {view === 'admin' && token && isAdmin && (
        <div className="auth-container" style={{ maxWidth: '700px' }}>
          <h2>🛠 Admin Panel</h2>
          <p className="admin-description">
            Use this panel to generate 10 fresh cat listings. The cats will be owned by the current admin account.
          </p>
          <div className="admin-stats">
            <div><strong>User:</strong> {user.firstName} {user.lastName}</div>
            <div><strong>Role:</strong> {user.role}</div>
            <div><strong>Email:</strong> {user.email}</div>
            <div><strong>Admin badge:</strong> <span className="role-badge">admin</span></div>
          </div>
          <button className="btn-primary" style={{ width: '100%', marginTop: '20px' }} onClick={handleSeedTenCats}>
            + Create 10 Cats
          </button>
          <button style={{ marginTop: '10px', width: '100%', padding: '10px', background: '#999', color: 'white', border: 'none', borderRadius: '5px', cursor: 'pointer' }} onClick={() => setView('cats')}>Back to Cats</button>

          <div style={{ marginTop: '30px' }}>
            <h3 style={{ marginBottom: '15px', color: '#667eea' }}>User Management</h3>
            {adminError && <div className="alert error" style={{ marginBottom: '15px' }}>{adminError}</div>}
            {adminLoading ? (
              <div className="loading" style={{ color: '#333' }}>Loading users...</div>
            ) : (
              <div style={{ overflowX: 'auto' }}>
                <table style={{ width: '100%', borderCollapse: 'collapse', background: 'rgba(255,255,255,0.95)', borderRadius: '10px' }}>
                  <thead>
                    <tr style={{ textAlign: 'left', borderBottom: '1px solid #e0e0e0' }}>
                      <th style={{ padding: '12px' }}>User</th>
                      <th style={{ padding: '12px' }}>Email</th>
                      <th style={{ padding: '12px' }}>Role</th>
                      <th style={{ padding: '12px' }}>Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {adminUsers.map((adminUser) => {
                      const isProtectedAdmin = adminUser.role === 'admin'
                      const isCurrentUser = user?.id === adminUser.id
                      return (
                        <tr key={adminUser.id} style={{ borderBottom: '1px solid #f0f0f0' }}>
                          <td style={{ padding: '12px' }}>
                            {adminUser.firstName} {adminUser.lastName}
                            {isProtectedAdmin && <span className="role-badge" style={{ marginLeft: '10px' }}>admin</span>}
                            {isCurrentUser && <span style={{ marginLeft: '10px', color: '#999', fontSize: '12px' }}>(you)</span>}
                          </td>
                          <td style={{ padding: '12px' }}>{adminUser.email}</td>
                          <td style={{ padding: '12px' }}>
                            {isProtectedAdmin ? (
                              <span className="role-badge">admin</span>
                            ) : (
                              <select
                                value={adminUser.role}
                                onChange={(e) => handleUpdateUserRole(adminUser.id, e.target.value)}
                              >
                                <option value="pet-adopter">pet-adopter</option>
                                <option value="care-giver">care-giver</option>
                              </select>
                            )}
                          </td>
                          <td style={{ padding: '12px' }}>
                            <button
                              className="btn-delete"
                              disabled={isProtectedAdmin}
                              onClick={() => handleDeleteUser(adminUser.id)}
                              style={{ opacity: isProtectedAdmin ? 0.5 : 1 }}
                            >
                              Delete
                            </button>
                          </td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
                {adminUsers.length === 0 && <div style={{ padding: '15px', color: '#666' }}>No users found.</div>}
              </div>
            )}
          </div>
        </div>
      )}

      {/* CHAT VIEW */}
      {view === 'chat' && token && canUseChat && (
        <div className="auth-container" style={{ maxWidth: '1100px' }}>
          <h2>💬 Chat</h2>
          <p className="admin-description" style={{ marginBottom: '15px' }}>
            Чатът е достъпен само между care-giver и pet-adopter.
          </p>

          {chatError && <div className="alert error" style={{ marginBottom: '15px' }}>{chatError}</div>}

          <div style={{ display: 'grid', gridTemplateColumns: '320px 1fr', gap: '20px', alignItems: 'stretch' }}>
            <div style={{ background: 'rgba(255,255,255,0.95)', borderRadius: '12px', padding: '15px', minHeight: '620px' }}>
              <div style={{ display: 'flex', gap: '8px', marginBottom: '15px' }}>
                <button className={chatTab === 'conversations' ? 'btn-primary' : 'btn-secondary'} onClick={() => setChatTab('conversations')} style={{ flex: 1 }}>
                  Разговори
                </button>
                <button className={chatTab === 'contacts' ? 'btn-primary' : 'btn-secondary'} onClick={() => setChatTab('contacts')} style={{ flex: 1 }}>
                  Контакти
                </button>
              </div>

              {chatLoading ? (
                <div className="loading" style={{ color: '#333' }}>Loading chat...</div>
              ) : chatTab === 'conversations' ? (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
                  {chatConversations.length === 0 ? (
                    <div style={{ color: '#666' }}>Няма разговори.</div>
                  ) : chatConversations.map((conversation) => (
                    <button
                      key={conversation.conversationId}
                      onClick={() => openConversation(conversation)}
                      style={{
                        textAlign: 'left',
                        padding: '12px',
                        borderRadius: '10px',
                        border: activeChat.conversationId === conversation.conversationId ? '2px solid #667eea' : '1px solid #e5e5e5',
                        background: activeChat.conversationId === conversation.conversationId ? '#f3f6ff' : 'white',
                        cursor: 'pointer'
                      }}
                    >
                      <div style={{ display: 'flex', justifyContent: 'space-between', gap: '10px' }}>
                        <strong>{conversation.otherUserFirstName} {conversation.otherUserLastName}</strong>
                        {conversation.unreadCount > 0 ? <span className="role-badge">{conversation.unreadCount}</span> : null}
                      </div>
                      <div style={{ fontSize: '12px', color: '#666' }}>{conversation.otherUserRole}</div>
                      <div style={{ marginTop: '6px', color: '#444', fontSize: '14px' }}>
                        {conversation.lastMessageContent || 'Без съобщения'}
                      </div>
                    </button>
                  ))}
                </div>
              ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
                  {chatContacts.length === 0 ? (
                    <div style={{ color: '#666' }}>Няма налични контакти.</div>
                  ) : chatContacts.map((contact) => {
                    const existingConversation = chatConversations.find((conversation) => conversation.otherUserId === contact.id)
                    return (
                      <button
                        key={contact.id}
                        onClick={() => openContactChat(contact)}
                        style={{
                          textAlign: 'left',
                          padding: '12px',
                          borderRadius: '10px',
                          border: existingConversation ? '2px solid #667eea' : '1px solid #e5e5e5',
                          background: existingConversation ? '#f3f6ff' : 'white',
                          cursor: 'pointer'
                        }}
                      >
                        <strong>{contact.firstName} {contact.lastName}</strong>
                        <div style={{ fontSize: '12px', color: '#666' }}>{contact.email}</div>
                        <div style={{ fontSize: '12px', color: '#666' }}>{contact.role}{contact.city ? ` · ${contact.city}` : ''}</div>
                      </button>
                    )
                  })}
                </div>
              )}
            </div>

            <div style={{ background: 'rgba(255,255,255,0.95)', borderRadius: '12px', padding: '15px', minHeight: '620px', display: 'flex', flexDirection: 'column' }}>
              {activeChat.receiver ? (
                <>
                  <div style={{ borderBottom: '1px solid #eee', paddingBottom: '12px', marginBottom: '12px' }}>
                    <h3 style={{ margin: 0 }}>{activeChat.receiver.firstName} {activeChat.receiver.lastName}</h3>
                    <div style={{ color: '#666', fontSize: '14px' }}>{activeChat.receiver.role}{activeChat.receiver.city ? ` · ${activeChat.receiver.city}` : ''}</div>
                  </div>

                  <div style={{ flex: 1, overflowY: 'auto', paddingRight: '4px', display: 'flex', flexDirection: 'column', gap: '10px' }}>
                    {chatMessages.length === 0 ? (
                      <div style={{ color: '#666', padding: '20px 0' }}>Започни разговора с първо съобщение.</div>
                    ) : chatMessages.map((message) => {
                      const isMine = message.senderId === user?.id
                      return (
                        <div key={message.id} style={{ display: 'flex', justifyContent: isMine ? 'flex-end' : 'flex-start' }}>
                          <div style={{
                            maxWidth: '75%',
                            padding: '12px 14px',
                            borderRadius: '14px',
                            background: isMine ? '#667eea' : '#f1f1f1',
                            color: isMine ? 'white' : '#222',
                            whiteSpace: 'pre-wrap'
                          }}>
                            <div>{message.content}</div>
                            <div style={{ fontSize: '11px', opacity: 0.75, marginTop: '6px' }}>
                              {new Date(message.sentAt).toLocaleString()}
                              {isMine ? (message.isRead ? ' · read' : ' · sent') : ''}
                            </div>
                          </div>
                        </div>
                      )
                    })}
                  </div>

                  <form onSubmit={handleSendChatMessage} style={{ marginTop: '15px' }}>
                    <div className="form-group" style={{ marginBottom: '10px' }}>
                      <label>Message</label>
                      <textarea
                        rows="4"
                        value={chatDraft}
                        onChange={(e) => setChatDraft(e.target.value)}
                        placeholder="Напиши съобщение..."
                        required
                      />
                    </div>
                    <input type="submit" value={chatSending ? 'Sending...' : 'Send'} disabled={chatSending} />
                  </form>
                </>
              ) : (
                <div style={{ color: '#666', marginTop: '20px' }}>
                  Избери разговор или контакт отляво, за да започнеш чат.
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

