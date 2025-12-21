import './globals.css'
import React from 'react'
import Header from '../components/Header'

export const metadata = {
  title: 'IIOT Fleet Telemetry Dashboard',
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body className="min-h-screen">
        <Header />
        {children}
      </body>
    </html>
  )
}
