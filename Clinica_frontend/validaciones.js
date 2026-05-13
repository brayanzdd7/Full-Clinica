// ============================================================
//  validaciones.js  —  Clínica Médica
//  Librería global de validaciones reutilizable
// ============================================================

const Validar = {

  // ── Reglas base ────────────────────────────────────────────
  soloLetras: (val) => /^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s'-]+$/.test(val.trim()),
  esEmail:    (val) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(val.trim()),
  esNumero:   (val) => !isNaN(val) && val.toString().trim() !== '',
  minLen:     (val, n) => val.trim().length >= n,
  noVacio:    (val) => val.trim() !== '',
  soloNums:   (val) => /^\d+$/.test(val.trim()),
  esFecha:    (val) => val !== '' && !isNaN(Date.parse(val)),
  esPositivo: (val) => parseFloat(val) >= 0,
  noTieneNumeros: (val) => !/\d/.test(val),

  // ── Mostrar error en un campo ──────────────────────────────
  marcarError(inputId, msg) {
    const el = document.getElementById(inputId);
    if (!el) return;
    el.style.borderColor = '#f87171';
    el.style.boxShadow   = '0 0 0 3px rgba(248,113,113,0.15)';
    let err = el.parentElement.querySelector('.v-err');
    if (!err) {
      err = document.createElement('div');
      err.className = 'v-err';
      err.style.cssText = 'color:#f87171;font-size:0.73rem;margin-top:4px;display:flex;align-items:center;gap:4px';
      el.parentElement.appendChild(err);
    }
    err.innerHTML = `<span>⚠</span> ${msg}`;
  },

  // ── Limpiar error de un campo ──────────────────────────────
  limpiarError(inputId) {
    const el = document.getElementById(inputId);
    if (!el) return;
    el.style.borderColor = '';
    el.style.boxShadow   = '';
    const err = el.parentElement.querySelector('.v-err');
    if (err) err.remove();
  },

  // ── Limpiar todos los errores de un formulario ─────────────
  limpiarTodos(ids) {
    ids.forEach(id => this.limpiarError(id));
  },

  // ── Validar campo en tiempo real (onblur / oninput) ────────
  bindRealTime(inputId, reglasArr) {
    const el = document.getElementById(inputId);
    if (!el) return;
    el.addEventListener('input', () => {
      const resultado = this.validarCampo(inputId, reglasArr);
      if (resultado === true) this.limpiarError(inputId);
    });
    el.addEventListener('blur', () => {
      this.validarCampo(inputId, reglasArr);
    });
    el.addEventListener('focus', () => {
      this.limpiarError(inputId);
    });
  },

  // ── Validar un solo campo con múltiples reglas ─────────────
  validarCampo(inputId, reglasArr) {
    const el = document.getElementById(inputId);
    if (!el) return true;
    const val = el.value;
    for (const regla of reglasArr) {
      if (!regla.fn(val)) {
        this.marcarError(inputId, regla.msg);
        return regla.msg;
      }
    }
    this.limpiarError(inputId);
    return true;
  },

  // ── Validar formulario completo y retornar true/false ──────
  validarFormulario(campos) {
    let ok = true;
    for (const [id, reglas] of Object.entries(campos)) {
      const resultado = this.validarCampo(id, reglas);
      if (resultado !== true) ok = false;
    }
    return ok;
  },

  // ── Reglas predefinidas listas para usar ──────────────────
  reglas: {
    nombre: [
      { fn: v => Validar.noVacio(v),          msg: 'El nombre es requerido.' },
      { fn: v => Validar.minLen(v, 2),         msg: 'Mínimo 2 caracteres.' },
      { fn: v => Validar.noTieneNumeros(v),    msg: 'El nombre no puede contener números.' },
      { fn: v => Validar.soloLetras(v),        msg: 'Solo letras y espacios permitidos.' },
    ],
    apellido: [
      { fn: v => Validar.noVacio(v),           msg: 'El apellido es requerido.' },
      { fn: v => Validar.minLen(v, 2),         msg: 'Mínimo 2 caracteres.' },
      { fn: v => Validar.noTieneNumeros(v),    msg: 'El apellido no puede contener números.' },
      { fn: v => Validar.soloLetras(v),        msg: 'Solo letras y espacios permitidos.' },
    ],
    email: [
      { fn: v => Validar.noVacio(v),           msg: 'El email es requerido.' },
      { fn: v => Validar.esEmail(v),           msg: 'Ingresa un email válido (ejemplo@correo.com).' },
    ],
    password: [
      { fn: v => Validar.noVacio(v),           msg: 'La contraseña es requerida.' },
      { fn: v => Validar.minLen(v, 6),         msg: 'Mínimo 6 caracteres.' },
    ],
    telefono: [
      { fn: v => v.trim() === '' || /^[\d\s\+\-\(\)]{7,15}$/.test(v), msg: 'Formato de teléfono inválido.' },
    ],
    monto: [
      { fn: v => Validar.noVacio(v),           msg: 'El monto es requerido.' },
      { fn: v => Validar.esNumero(v),          msg: 'Ingresa un número válido.' },
      { fn: v => Validar.esPositivo(v),        msg: 'El monto debe ser mayor a 0.' },
    ],
    requerido: [
      { fn: v => Validar.noVacio(v),           msg: 'Este campo es requerido.' },
    ],
    fecha: [
      { fn: v => Validar.noVacio(v),           msg: 'La fecha es requerida.' },
      { fn: v => Validar.esFecha(v),           msg: 'Fecha inválida.' },
    ],
    licencia: [
      { fn: v => Validar.noVacio(v),           msg: 'El número de licencia es requerido.' },
      { fn: v => Validar.minLen(v, 4),         msg: 'Mínimo 4 caracteres.' },
    ],
  },

  // ── Confirmación de contraseña ─────────────────────────────
  confirmarPassword(passId, confirmId) {
    const pass    = document.getElementById(passId)?.value || '';
    const confirm = document.getElementById(confirmId)?.value || '';
    if (confirm === '') {
      this.marcarError(confirmId, 'Confirma tu contraseña.');
      return false;
    }
    if (pass !== confirm) {
      this.marcarError(confirmId, 'Las contraseñas no coinciden.');
      return false;
    }
    this.limpiarError(confirmId);
    return true;
  },

  // ── Toast de notificación global ───────────────────────────
  toast(msg, tipo = 'ok') {
    const colores = { ok: '#0fa8a0', error: '#f87171', warn: '#f59e0b' };
    const t = document.createElement('div');
    t.style.cssText = `
      position:fixed;bottom:2rem;right:2rem;
      background:#0e1a22;border:1px solid ${colores[tipo]};
      color:#e8f4f3;padding:0.8rem 1.2rem;border-radius:12px;
      font-size:0.85rem;z-index:9999;
      box-shadow:0 4px 20px rgba(0,0,0,0.4);
      font-family:'DM Sans',sans-serif;
      animation:toastIn 0.3s ease;
    `;
    if (!document.querySelector('#toast-style')) {
      const s = document.createElement('style');
      s.id = 'toast-style';
      s.textContent = '@keyframes toastIn{from{opacity:0;transform:translateY(20px)}to{opacity:1;transform:translateY(0)}}';
      document.head.appendChild(s);
    }
    t.textContent = msg;
    document.body.appendChild(t);
    setTimeout(() => t.remove(), 3500);
  },
};