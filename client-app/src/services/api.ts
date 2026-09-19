import axios from 'axios'
import { refreshSession } from './session'

const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
  headers: {
    'Content-Type': 'application/json',
  },
})

// Add auth token to requests
api.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('token')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  undefined,
  { synchronous: true },
)

// A rejected request has not run its controller action. Renew once, then retry it.
api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const request = error.config
    if (error.response?.status === 401 && request && !request._sessionRetried) {
      request._sessionRetried = true
      let token: string
      try {
        const currentToken = localStorage.getItem('token')
        // Another concurrent request (or tab) may already have renewed the token.
        token =
          currentToken && request.headers.Authorization !== `Bearer ${currentToken}`
            ? currentToken
            : await refreshSession()
      } catch (renewalError) {
        if (axios.isAxiosError(renewalError) && renewalError.response?.status === 401) {
          error.response.data = {
            message:
              'Your session has expired. Keep this editor open, sign in in another tab, then save again.',
          }
        }
        return Promise.reject(error)
      }
      request.headers.Authorization = `Bearer ${token}`
      return api.request(request)
    }
    // Never navigate away here: an authentication failure must not discard a draft.
    return Promise.reject(error)
  },
)

export default api
