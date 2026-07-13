import './globals.css'
import React from 'react'

export const metadata = {
  title: 'IIOT Fleet Telemetry Dashboard',
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body className="min-h-screen">
        {children}
      </body>
    </html>
  )
}
