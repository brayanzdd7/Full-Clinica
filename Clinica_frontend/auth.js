const AUTH = {
  token: () => localStorage.getItem('token'),
  rol: () => localStorage.getItem('rol') || '',
  nombre: () => localStorage.getItem('nombre') || '',
  apellido: () => localStorage.getItem('apellido') || '',
  usuarioId: () => localStorage.getItem('usuarioId'),
  doctorId: () => localStorage.getItem('doctorId'),
  pacienteId: () => localStorage.getItem('pacienteId'),

  esAdmin: () => ['Administrador', 'Admin'].includes(AUTH.rol()),
  esDoctor: () => AUTH.rol() === 'Doctor',
  esPaciente: () => AUTH.rol() === 'Paciente',
  esRecep: () => AUTH.rol() === 'Recepcionista',
  esAdminORecep: () => AUTH.esAdmin() || AUTH.esRecep(),

  headers: () => ({
    'Authorization': `Bearer ${AUTH.token()}`,
    'Content-Type': 'application/json'
  }),

  guard(rolesPermitidos = []) {
    if (!AUTH.token()) {
      window.location.replace('index.html');
      return false;
    }

    if (rolesPermitidos.length) {
      const r = AUTH.rol();
      const coincide = (permitido) => {
        if (permitido === r) return true;
        if ((permitido === 'Administrador' || permitido === 'Admin') && (r === 'Administrador' || r === 'Admin')) return true;
        return false;
      };

      if (!rolesPermitidos.some(coincide)) {
        window.location.replace('dashboard.html');
        return false;
      }
    }

    return true;
  },

  logout() {
    localStorage.removeItem('token');
    localStorage.removeItem('rol');
    localStorage.removeItem('nombre');
    localStorage.removeItem('apellido');
    localStorage.removeItem('usuarioId');
    localStorage.removeItem('doctorId');
    localStorage.removeItem('pacienteId');
    window.location.replace('index.html');
  },

  buildSidebar(activePage = '') {
    const rol = AUTH.rol();

    const navPrincipal = [
      { href: 'dashboard.html', label: 'Dashboard', icon: 'grid', roles: ['Administrador','Admin','Doctor','Paciente','Recepcionista'] },
      { href: 'citas.html', label: 'Citas', icon: 'calendar', roles: ['Administrador','Admin','Doctor','Paciente','Recepcionista'] },
      { href: 'mis-recetas.html', label: 'Mis recetas', icon: 'rx', roles: ['Paciente'] },
      { href: 'pacientes.html', label: 'Pacientes', icon: 'users', roles: ['Administrador','Admin','Doctor','Recepcionista'] },
      { href: 'doctores.html', label: 'Doctores', icon: 'doctor', roles: ['Administrador','Admin','Recepcionista'] },
    ];

    const navGestion = [
      { href: 'medicamentos.html', label: 'Medicamentos', icon: 'pill', roles: ['Administrador','Admin','Doctor'] },
      { href: 'recetas-internas.html', label: 'Recetas', icon: 'rx', roles: ['Administrador','Admin','Recepcionista'] },
      { href: 'pagos.html', label: 'Pagos', icon: 'card', roles: ['Administrador','Admin','Recepcionista'] },
      { href: 'facturacion.html', label: 'Facturación', icon: 'invoice', roles: ['Administrador','Admin','Recepcionista'] },
    ];

    const svgs = {
      grid: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="14" y="14" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/></svg>`,
      calendar: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg>`,
      users: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>`,
      doctor: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 2a5 5 0 1 0 0 10A5 5 0 0 0 12 2z"/><path d="M20 22a8 8 0 0 0-16 0"/><line x1="12" y1="15" x2="12" y2="19"/><line x1="10" y1="17" x2="14" y2="17"/></svg>`,
      pill: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="8" width="18" height="8" rx="4"/><line x1="12" y1="8" x2="12" y2="16"/></svg>`,
      card: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="1" y="4" width="22" height="16" rx="2"/><line x1="1" y1="10" x2="23" y2="10"/></svg>`,
      invoice: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg>`,
      rx: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 4h7l2 6-2 6H4"/><path d="M20 4h-4"/><path d="M16 12h4"/><path d="M20 20h-4"/></svg>`,
    };

    const renderItems = (items) => items
      .filter(i => i.roles.includes(rol))
      .map(i => `<a href="${i.href}" class="nav-item ${activePage === i.href ? 'active' : ''}">${svgs[i.icon]}${i.label}</a>`)
      .join('');

    const roleBadgeColor = {
      'Administrador': 'background:rgba(168,85,247,0.15);color:#c084fc',
      'Admin': 'background:rgba(168,85,247,0.15);color:#c084fc',
      'Doctor': 'background:rgba(15,168,160,0.15);color:#0fa8a0',
      'Paciente': 'background:rgba(59,130,246,0.15);color:#60a5fa',
      'Recepcionista': 'background:rgba(245,158,11,0.15);color:#f59e0b',
    }[rol] || '';

    const gestionItems = renderItems(navGestion);

    return `
      <div class="sidebar-logo">
        <div class="logo-box">🏥</div>
        <div class="logo-text">Clínica Médica<small>Panel de Control</small></div>
      </div>
      <div class="nav-section">
        <div class="nav-label">Principal</div>
        ${renderItems(navPrincipal)}
      </div>
      ${gestionItems ? `<div class="nav-section"><div class="nav-label">Gestión</div>${gestionItems}</div>` : ''}
      <div class="sidebar-footer">
        <div class="user-card">
          <div class="avatar">${(AUTH.nombre()).charAt(0).toUpperCase()}</div>
          <div class="user-info">
            <div class="user-name">${AUTH.nombre()} ${AUTH.apellido()}</div>
            <div class="user-role" style="font-size:0.68rem;padding:2px 6px;border-radius:8px;display:inline-block;${roleBadgeColor}">${rol}</div>
          </div>
          <button class="btn-logout" onclick="AUTH.logout()" title="Cerrar sesión">↩</button>
        </div>
      </div>`;
  },

  initSidebar(activePage = '') {
    const sidebar = document.getElementById('sidebar');
    if (sidebar) sidebar.innerHTML = AUTH.buildSidebar(activePage);
  }
};