import { Home, Upload, Network, BarChart3, Settings } from 'lucide-react';
import { cn } from '../../lib/utils';
import { useUIStore } from '../../store/uiStore';
import { useState } from 'react';

type NavItem = {
  icon: typeof Home;
  label: string;
  id: string;
};

const navItems: NavItem[] = [
  { icon: Home, label: 'Dashboard', id: 'dashboard' },
  { icon: Upload, label: 'Documents', id: 'documents' },
  { icon: Network, label: 'Network Graph', id: 'network' },
  { icon: BarChart3, label: 'Analytics', id: 'analytics' },
  { icon: Settings, label: 'Settings', id: 'settings' },
];

export function Sidebar() {
  const { sidebarOpen } = useUIStore();
  const [activeTab, setActiveTab] = useState('documents');

  if (!sidebarOpen) return null;

  return (
    <aside className="w-64 border-r bg-background">
      <nav className="flex flex-col gap-2 p-4">
        {navItems.map((item) => {
          const Icon = item.icon;
          return (
            <button
              key={item.id}
              onClick={() => setActiveTab(item.id)}
              className={cn(
                "flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors",
                activeTab === item.id
                  ? "bg-primary text-primary-foreground"
                  : "hover:bg-accent hover:text-accent-foreground"
              )}
            >
              <Icon className="h-5 w-5" />
              {item.label}
            </button>
          );
        })}
      </nav>
    </aside>
  );
}
