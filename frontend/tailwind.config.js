/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./app/**/*.{ts,tsx,js,jsx}", "./components/**/*.{ts,tsx,js,jsx}"],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        primary: '#f9f506',
        'background-dark': '#0a0a0a',
        'surface-dark': '#121212',
        'border-dark': '#283639'
      },
      fontFamily: {
        display: ["Space Grotesk", 'sans-serif']
      }
    }
  },
  plugins: []
}
