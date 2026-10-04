// ── FloodLink AI — App Shell ─────────────────────────────────────────────────
//
// Implements the Carbon-inspired fixed left SideNav + compact top Header layout
// defined in the FloodLink UI spec.
//
// Layout:
//   ┌─────────────────────────────────────────────────┐
//   │ fl-header (fixed, 48px, dark #161616)            │
//   ├────────────┬────────────────────────────────────┤
//   │ fl-sidenav │ fl-workspace                        │
//   │ (fixed,    │ (scrollable, bg-base)               │
//   │  256px)    │                                     │
//   └────────────┴────────────────────────────────────┘
//
// Carbon components used:
//   - Header, HeaderName, HeaderGlobalBar, HeaderGlobalAction (top bar)
//   - SideNav, SideNavItems, SideNavLink (left nav — using Carbon's SideNav)
//   - Content (main workspace wrapper)
// ─────────────────────────────────────────────────────────────────────────────

import { useState } from 'react';
import {
  Header,
  HeaderName,
  HeaderGlobalBar,
  HeaderGlobalAction,
  SideNav,
  SideNavItems,
  SideNavLink,
  Content,
  Theme,
} from '@carbon/react';
import {
  Notification,
  UserAvatar,
  Dashboard,
  Report,
  InventoryManagement,
  Location,
  CheckmarkOutline,
  TrafficFlow,
  Terminal,
} from '@carbon/icons-react';
import { NAV_ITEMS, SECTION_LABELS, PageId, NavItem } from '../nav';
import InventoryDashboard from '../pages/InventoryDashboard.tsx';
import DepotManagement from '../pages/DepotManagement';
import PlaceholderPage from '../pages/PlaceholderPage';

// Map PageId → Carbon icon component
const PAGE_ICONS: Record<PageId, React.ElementType> = {
  dashboard:  Dashboard,
  incidents:  Report,
  inventory:  InventoryManagement,
  depots:     Location,
  approvals:  CheckmarkOutline,
  routes:     TrafficFlow,
  logs:       Terminal,
};

function NavSection({ label, items, activePage, onNav }: {
  label: string;
  items: NavItem[];
  activePage: PageId;
  onNav: (id: PageId) => void;
}) {
  return (
    <>
      <p className="fl-sidenav__label">{label}</p>
      {items.map(item => {
        const Icon = PAGE_ICONS[item.id];
        return (
          <SideNavLink
            key={item.id}
            className="fl-sidenav__item"
            renderIcon={Icon ? (props: any) => <Icon {...props} size={16} /> : undefined}
            isActive={activePage === item.id}
            onClick={() => onNav(item.id)}
            href="#"
            aria-current={activePage === item.id ? 'page' : undefined}
          >
            {item.label}
            {item.badge ? (
              <span className="fl-sidenav__badge" aria-label={`${item.badge} pending`}>
                {item.badge}
              </span>
            ) : null}
          </SideNavLink>
        );
      })}
    </>
  );
}

function renderPage(page: PageId) {
  switch (page) {
    case 'inventory': return <InventoryDashboard />;
    case 'depots':    return <DepotManagement />;
    default:          return <PlaceholderPage pageId={page} />;
  }
}

export default function AppShell() {
  const [activePage, setActivePage] = useState<PageId>('inventory');

  // Group nav items by section for the sidebar
  const sections = ['operations', 'management', 'audit'] as const;

  return (
    // Wrap in Carbon's white g10 theme — our token overrides layer on top via CSS
    <Theme theme="g10">
      {/* ── Top Header ────────────────────────────────────────────────────── */}
      <Header className="fl-header" aria-label="FloodLink AI">
        <HeaderName className="fl-header__brand" prefix="" href="#" onClick={e => { e.preventDefault(); setActivePage('dashboard'); }}>
          <span className="fl-header__mark" aria-hidden="true">FL</span>
          <span className="fl-header__name">FloodLinkAI</span>
        </HeaderName>

        <HeaderGlobalBar className="fl-header__actions">
          <span className="fl-header__status">
            <span className="fl-header__status-dot" aria-hidden="true" />
            System online
          </span>
          <HeaderGlobalAction className="fl-header__icon-btn" aria-label="Notifications" tooltipAlignment="end">
            <Notification size={20} />
          </HeaderGlobalAction>
          <HeaderGlobalAction className="fl-header__icon-btn" aria-label="Account" tooltipAlignment="end">
            <UserAvatar size={20} />
          </HeaderGlobalAction>
        </HeaderGlobalBar>
      </Header>

      {/* ── Left Sidebar ─────────────────────────────────────────────────── */}
      <SideNav
        className="fl-sidenav"
        aria-label="Side navigation"
        isFixedNav
        expanded
        isPersistent
      >
        <SideNavItems>
          {sections.map(section => {
            const items = NAV_ITEMS.filter(i => i.section === section);
            if (!items.length) return null;
            return (
              <div key={section} className="fl-sidenav__section">
                <NavSection
                  label={SECTION_LABELS[section]}
                  items={items}
                  activePage={activePage}
                  onNav={setActivePage}
                />
              </div>
            );
          })}
        </SideNavItems>
      </SideNav>

      {/* ── Main Workspace ───────────────────────────────────────────────── */}
      <Content className="fl-workspace" id="main-content">
        {renderPage(activePage)}
      </Content>
    </Theme>
  );
}
