import { Link, NavLink, useLocation, useNavigate } from 'react-router-dom'
import {
  Header,
  HeaderContainer,
  HeaderGlobalAction,
  HeaderGlobalBar,
  HeaderMenu,
  HeaderMenuButton,
  HeaderMenuItem,
  HeaderName,
  HeaderNavigation,
  HeaderSideNavItems,
  SideNav,
  SideNavItems,
  SkipToContent,
} from '@carbon/react'
import { Logout } from '@carbon/icons-react'
import { useAuth } from '../context/AuthContext'
import { PAGES, PAGE_GROUPS, canOpen, type PageDef } from '../routes'

/**
 * App header (Carbon UI Shell): the pages the signed-in role may open, grouped into menus
 * (Operations, Stock, Insights, Admin), the signed-in user and sign-out. On small screens the
 * menus move into a side navigation opened from the menu button. A group with one page is a plain link.
 */
export function Navbar() {
  const { user, role, logout } = useAuth()
  const navigate = useNavigate()
  const { pathname } = useLocation()
  const groups = PAGE_GROUPS
    .map((group) => ({ group, pages: PAGES.filter((p) => p.group === group && canOpen(p, role)) }))
    .filter((g) => g.pages.length > 0)

  const signOut = () => {
    logout()
    navigate('/login', { replace: true })
  }

  const isCurrent = (page: PageDef) => pathname === page.path || pathname.startsWith(`${page.path}/`)

  const item = (page: PageDef) => (
    <HeaderMenuItem key={page.path} as={NavLink} to={page.path} isActive={isCurrent(page)}
      aria-current={isCurrent(page) ? 'page' : undefined}>
      {page.label}
    </HeaderMenuItem>
  )

  const menus = groups.map(({ group, pages }) =>
    pages.length === 1 ? item(pages[0]) : (
      <HeaderMenu key={group} aria-label={group} menuLinkName={group} isActive={pages.some(isCurrent)}>
        {pages.map(item)}
      </HeaderMenu>
    ))

  return (
    <HeaderContainer
      render={({ isSideNavExpanded, onClickSideNavExpand }: { isSideNavExpanded: boolean; onClickSideNavExpand: () => void }) => (
        <Header aria-label="FloodLink AI Relief Coordination Portal">
          <SkipToContent href="#main-content" />
          {user && (
            <HeaderMenuButton
              aria-label={isSideNavExpanded ? 'Close menu' : 'Open menu'}
              onClick={onClickSideNavExpand}
              isActive={isSideNavExpanded}
              aria-expanded={isSideNavExpanded}
            />
          )}
          <HeaderName as={Link} to="/" prefix="FloodLink AI">
            Relief Coordination Portal
          </HeaderName>
          {user && (
            <>
              <HeaderNavigation aria-label="Main">{menus}</HeaderNavigation>
              <HeaderGlobalBar>
                <span className="fl-header-user">
                  {user.name} · <span className="fl-header-role">{role}</span>
                </span>
                <HeaderGlobalAction aria-label="Sign out" tooltipAlignment="end" onClick={signOut}>
                  <Logout size={20} />
                </HeaderGlobalAction>
              </HeaderGlobalBar>
              <SideNav aria-label="Side navigation" expanded={isSideNavExpanded} isPersistent={false}
                onSideNavBlur={onClickSideNavExpand}>
                <SideNavItems>
                  <HeaderSideNavItems>{menus}</HeaderSideNavItems>
                </SideNavItems>
              </SideNav>
            </>
          )}
        </Header>
      )}
    />
  )
}
