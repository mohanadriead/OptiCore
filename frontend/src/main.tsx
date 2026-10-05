import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { App } from './app/App'
import { appDirection } from './app/direction'
import './index.css'
document.documentElement.dir = appDirection
createRoot(document.getElementById('root')!).render(<StrictMode><App /></StrictMode>)
