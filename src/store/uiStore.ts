import { create } from 'zustand';

interface UIStore {
  theme: 'light' | 'dark';
  sidebarOpen: boolean;
  viewMode: 'grid' | 'list';

  // Actions
  toggleTheme: () => void;
  toggleSidebar: () => void;
  setViewMode: (mode: 'grid' | 'list') => void;
}

export const useUIStore = create<UIStore>((set) => ({
  theme: 'light',
  sidebarOpen: true,
  viewMode: 'grid',

  toggleTheme: () =>
    set((state) => ({
      theme: state.theme === 'light' ? 'dark' : 'light',
    })),

  toggleSidebar: () =>
    set((state) => ({
      sidebarOpen: !state.sidebarOpen,
    })),

  setViewMode: (mode) =>
    set({ viewMode: mode }),
}));
