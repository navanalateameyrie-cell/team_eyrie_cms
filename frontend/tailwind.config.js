/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      fontFamily: {
        sans: ['"Inter"', 'ui-sans-serif', 'system-ui', '-apple-system', 'sans-serif'],
        display: ['"Inter"', 'ui-sans-serif', 'system-ui', '-apple-system', 'sans-serif'],
        mono: ['ui-monospace', 'SFMono-Regular', 'Menlo', 'Monaco', 'Consolas', 'monospace'],
      },
      colors: {
        // Classic Corporate Navy Hierarchy
        navy: {
          50: "#f0f4f9",
          100: "#e2eaf4",
          200: "#c1d0e4",
          300: "#91a7c7",
          400: "#627ea9",
          500: "#46628d",
          600: "#344c71",
          700: "#263b5a", // Secondary Navy
          800: "#172b4d", // Primary Navy
          900: "#10213a", // Deep Navy
          950: "#0a1424",
        },
        // Muted Corporate Gold Accent
        gold: {
          50: "#faf7f0",
          100: "#f5eedf",
          200: "#ebdcbE",
          300: "#dbc297",
          400: "#c5a572",
          500: "#b08d57", // Primary Muted Gold
          600: "#967440",
          700: "#7b5c2d",
          800: "#604620",
          900: "#453114",
        },
        // Backward-compatible Brand tokens mapped to refined executive palette
        brand: {
          50: "#faf7f0",
          100: "#f5eedf",
          200: "#ebdcbE",
          300: "#dbc297",
          400: "#c5a572",
          500: "#b08d57",
          600: "#967440",
          700: "#7b5c2d",
          800: "#172b4d",
          900: "#10213a",
        },
        // Enterprise Surface & Neutral tokens
        slate: {
          50: "#f5f6f8",  // Page Background
          100: "#edf0f5",
          200: "#e5e9ef", // Border
          300: "#d3d9e3",
          400: "#98a2b3",
          500: "#697586", // Secondary Text
          600: "#4b5565",
          700: "#364152",
          800: "#202b3c", // Primary Text
          900: "#121926",
          950: "#080c14",
        },
        // Semantic Financial & Operational Indicators
        emerald: {
          50: "#edfcf2",
          100: "#d3f8df",
          500: "#16845b", // Success
          600: "#126e4b",
          700: "#0e563b",
        },
        amber: {
          50: "#fef9ee",
          100: "#fdf0d2",
          500: "#c58a32", // Warning
          600: "#a97224",
          700: "#865818",
        },
        rose: {
          50: "#fdf2f2",
          100: "#fce4e4",
          500: "#c44d4d", // Danger
          600: "#a83a3a",
          700: "#892c2c",
        },
        sky: {
          50: "#f0f7ff",
          100: "#e0effe",
          500: "#3976b9", // Information
          600: "#2d5f99",
          700: "#224775",
        },
      },
      boxShadow: {
        subtle: "0 1px 2px 0 rgba(16, 33, 58, 0.04)",
        card: "0 1px 3px 0 rgba(16, 33, 58, 0.06), 0 1px 2px 0 rgba(16, 33, 58, 0.03)",
        "card-hover": "0 4px 12px 0 rgba(16, 33, 58, 0.08), 0 1px 3px 0 rgba(16, 33, 58, 0.04)",
        dropdown: "0 10px 25px -3px rgba(16, 33, 58, 0.12), 0 4px 6px -2px rgba(16, 33, 58, 0.05)",
      },
      borderRadius: {
        DEFAULT: "0.375rem",
        sm: "0.25rem",
        md: "0.375rem",
        lg: "0.5rem",
        xl: "0.75rem",
        "2xl": "1rem",
      },
    },
  },
  plugins: [],
};
