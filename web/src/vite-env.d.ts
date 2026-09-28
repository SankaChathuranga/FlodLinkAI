/// <reference types="vite/client" />

import type { ComponentType } from 'react'

declare module '*.jsx' {
  const component: ComponentType
  export default component
}
