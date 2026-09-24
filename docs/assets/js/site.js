// Mobile sidebar toggle + active-link highlight + copy buttons (no deps).
(function () {
  var sidebar = document.getElementById('sidebar');
  var scrim = document.getElementById('scrim');
  var btn = document.getElementById('menuBtn');
  function close() { if (sidebar) sidebar.classList.add('hidden-mobile'); if (scrim) scrim.hidden = true; }
  function open() { if (sidebar) sidebar.classList.remove('hidden-mobile'); if (scrim) scrim.hidden = false; }
  if (btn) btn.addEventListener('click', function () {
    if (sidebar && sidebar.classList.contains('hidden-mobile')) open(); else close();
  });
  if (scrim) scrim.addEventListener('click', close);
  // Active link: match longest href suffix against current path.
  var links = Array.prototype.slice.call(document.querySelectorAll('.nav-link'));
  var path = location.pathname.replace(/\\/g, '/');
  var best = null, bestLen = -1;
  links.forEach(function (a) {
    var href = a.getAttribute('href') || '';
    // resolve relative hrefs against current dir
    var tmp = document.createElement('a'); tmp.href = href;
    var p = tmp.pathname.replace(/\\/g, '/');
    if (path.endsWith(p) && p.length > bestLen) { best = a; bestLen = p.length; }
  });
  if (best) { best.classList.add('active'); best.setAttribute('aria-current', 'page'); }
  // Copy buttons: data-copy target selector or text.
  document.querySelectorAll('[data-copy]').forEach(function (b) {
    b.addEventListener('click', function () {
      var t = b.getAttribute('data-copy') || '';
      var done = function () { var o = b.textContent; b.textContent = 'Copied'; setTimeout(function(){ b.textContent = o; }, 1200); };
      if (t.charAt(0) === '#') { var el = document.querySelector(t); if (el) navigator.clipboard.writeText(el.textContent.trim()).then(done, done); }
      else navigator.clipboard.writeText(t).then(done, done);
    });
  });
})();
