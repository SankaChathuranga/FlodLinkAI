/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        // FloodLink brand colors — extend as the team agrees on the design system
        floodlink: {
          blue: '#0369a1',
          teal: '#0d9488',
        }
      }
    },
  },
  plugins: [],
}
