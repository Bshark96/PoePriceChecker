// Main PoE 1 Overlay Application Orchestrator
(function() {
  // Global Controller & Component Instances
  var bannerRenderer = null;
  var numpadFlyout = null;
  var countdownTimer = null;
  var itemController = null;
  var tradeController = null;
  var liveSearchController = null;
  var alarmController = null;
  var viewController = null;

  function initApp() {
    // Instantiate UI Renderers and Components
    bannerRenderer = new BannerRenderer('itemBanner');
    numpadFlyout = new NumpadFlyout('numpadFlyout');
    countdownTimer = new CountdownTimer(5.0, 'autoMinimizeTimer', 'bottomProgressBar', 'overlayWrapper');

    // Instantiate Domain Controllers
    viewController = new ViewController();
    itemController = new ItemController(bannerRenderer, numpadFlyout);
    tradeController = new TradeController(new TradeCardRenderer());
    liveSearchController = new LiveSearchController();
    alarmController = new AlarmController();

    // Expose for Bridge & C# WebView interoperability
    window.bannerRenderer = bannerRenderer;
    window.numpadFlyout = numpadFlyout;
    window.countdownTimer = countdownTimer;
    window.viewController = viewController;
    window.itemController = itemController;
    window.tradeController = tradeController;
    window.liveSearchController = liveSearchController;
    window.alarmController = alarmController;

    // Attach Event Listeners via Event Delegation (No inline HTML events)
    initEventDelegation();

    // Initialize View
    viewController.init();

    // Corrupted filter text toggle
    var corruptedBadge = document.getElementById('corruptedBadge');
    if (corruptedBadge) {
      corruptedBadge.addEventListener('click', function(e) {
        e.stopPropagation();
        corruptedBadge.classList.toggle('is-selected');
      });
    }

    // Global Web-to-C# Hover Sync
    if (!window.__poeHoverTrackerInstalled) {
      window.__poeHoverTrackerInstalled = true;
      var lastMoveSent = 0;
      var lastX = -1;
      var lastY = -1;
      var isPointerInside = false;

      function notifyHover(isHovered, eventType, x, y) {
        if (window.PoeBridge) {
          PoeBridge.notifyHost({
            action: 'set_hover',
            isHovered: isHovered,
            eventType: eventType,
            x: typeof x === 'number' ? x : 0,
            y: typeof y === 'number' ? y : 0
          });
        }
      }

      document.addEventListener('pointerenter', function(e) {
        isPointerInside = true;
        lastX = e.clientX;
        lastY = e.clientY;
        notifyHover(true, 'enter', e.clientX, e.clientY);
      }, true);

      document.addEventListener('pointermove', function(e) {
        var now = Date.now();
        var dx = Math.abs(e.clientX - lastX);
        var dy = Math.abs(e.clientY - lastY);
        var hostPaused = !!(window.PoeBridge && window.PoeBridge.hostTimerPaused);

        if (!isPointerInside || (!hostPaused && (dx >= 2 || dy >= 2) && (now - lastMoveSent >= 50))) {
          isPointerInside = true;
          lastX = e.clientX;
          lastY = e.clientY;
          lastMoveSent = now;
          notifyHover(true, 'move', e.clientX, e.clientY);
        }
      }, { passive: true, capture: true });

      document.addEventListener('pointerdown', function(e) {
        isPointerInside = true;
        notifyHover(true, 'interact', e.clientX, e.clientY);
      }, true);

      document.addEventListener('wheel', function(e) {
        isPointerInside = true;
        notifyHover(true, 'interact', e.clientX, e.clientY);
      }, { passive: true, capture: true });

      document.addEventListener('keydown', function(e) {
        if (e.key !== 'Escape') {
          notifyHover(true, 'interact', lastX >= 0 ? lastX : 0, lastY >= 0 ? lastY : 0);
        }
      }, true);

      document.documentElement.addEventListener('mouseleave', function(e) {
        if (e.relatedTarget === null) {
          isPointerInside = false;
          notifyHover(false, 'leave', e.clientX, e.clientY);
        }
      });
    }

    var isGameBar = window.PoeBridge && window.PoeBridge.isGameBarHost();
    if (isGameBar) {
      // Hide mock preset buttons in Game Bar overlay
      var presetBtns = document.querySelectorAll('.nav-btn-preset, .nav-btn-zip');
      for (var i = 0; i < presetBtns.length; i++) {
        presetBtns[i].style.display = 'none';
      }

      // Initialize controllers in real/live mode without mock data
      itemController.showWaitingState();
      liveSearchController.initReal();
      alarmController.initReal();
      // Countdown timer stays idle until real item is received
    } else {
      // Standalone browser preview mode: load mock preset for design preview
      itemController.loadPreset('chambers');
      liveSearchController.init();
      alarmController.init();
      countdownTimer.start();
    }

    // Signal C# UWP Host that Web UI is Ready
    if (window.PoeBridge) {
      window.PoeBridge.notifyHost({ action: 'ready', timestamp: Date.now() });
    }
  }

  function initEventDelegation() {
    var overlay = document.getElementById('overlayWrapper') || document.body;

    // Click Delegation
    overlay.addEventListener('click', function(e) {
      var corr = e.target.closest('#corruptedBadge');
      if (corr) {
        e.stopPropagation();
        corr.classList.toggle('is-selected');
        return;
      }

      var target = e.target;
      var btn = target.closest('button, [data-action], [data-view], [data-preset], [data-num]');
      if (!btn) return;

      // Tab Switching
      var viewMode = btn.getAttribute('data-view');
      if (viewMode) {
        viewController.switchView(viewMode);
        return;
      }

      // Preset Loading
      var presetKey = btn.getAttribute('data-preset');
      if (presetKey) {
        itemController.loadPreset(presetKey);
        return;
      }

      // Actions
      var action = btn.getAttribute('data-action');
      if (action) {
        switch (action) {
          case 'search':
            tradeController.triggerSearch();
            break;
          case 'add-live':
            liveSearchController.addNewLiveSearch();
            break;
          case 'paste-live':
            liveSearchController.pasteFromClipboard();
            break;
          case 'clear-live-history':
            liveSearchController.clearHistory(false);
            break;
          case 'delete-live':
            var liveId = btn.getAttribute('data-id');
            if (liveId) liveSearchController.removeMonitoredSearch(liveId);
            break;
          case 'add-alarm':
            alarmController.addRule();
            break;
          case 'delete-alarm':
            var alarmId = btn.getAttribute('data-id');
            if (alarmId) alarmController.removeRule(alarmId);
            break;
          case 'to-hideout':
            var seller = btn.getAttribute('data-seller');
            var token = btn.getAttribute('data-token');
            if (seller) tradeController.toHideout(seller, token);
            break;
          case 'preview-item':
            var itemJson = btn.getAttribute('data-item-json');
            if (itemJson) {
              try {
                var parsedItem = JSON.parse(decodeURIComponent(itemJson));
                openItemPreviewModalFromBackend(parsedItem);
              } catch (ex) {}
            }
            break;
          case 'close-preview':
            closeItemPreviewModal();
            break;
          case 'alert-hideout':
            alarmController.toHideoutFromAlert();
            break;
          case 'alert-dismiss':
            alarmController.dismissCurrentAlert();
            break;
          case 'restore':
            if (window.PoeBridge) {
              window.PoeBridge.restoreWidget();
            }
            break;
          case 'clear-num':
            numpadFlyout.clear();
            break;
          case 'close-numpad':
            numpadFlyout.close();
            break;
        }
        return;
      }

      // Numpad Numbers
      var numDigit = btn.getAttribute('data-num');
      if (numDigit !== null && numDigit !== undefined) {
        numpadFlyout.pressNum(numDigit);
        return;
      }
    });

    // Keydown Delegation
    overlay.addEventListener('keydown', function(e) {
      if (e.target && e.target.id === 'liveSearchInput' && (e.key === 'Enter' || e.keyCode === 13)) {
        liveSearchController.addNewLiveSearch();
      }
    });

    // Global CTRL+D shortcut to evaluate item and ALT+A shortcut to open Live Search view
    window.addEventListener('keydown', function(e) {
      if ((e.ctrlKey || e.metaKey) && !e.altKey && (e.key === 'd' || e.key === 'D' || e.keyCode === 68)) {
        e.preventDefault();
        if (window.PoeBridge) {
          window.PoeBridge.notifyHost({ action: 'evaluate' });
        }
        if (tradeController) {
          tradeController.triggerSearch();
        }
      } else if (e.altKey && !e.ctrlKey && !e.metaKey && (e.key === 'a' || e.key === 'A' || e.keyCode === 65)) {
        e.preventDefault();
        if (viewController) {
          viewController.switchView('live');
        }
        if (window.PoeBridge) {
          window.PoeBridge.notifyHost({ action: 'open_live_search' });
        }
      }
    });

    // Suppress standard right-click context menu in all views; open custom Numpad Flyout on PoE inputs
    document.addEventListener('contextmenu', function(e) {
      e.preventDefault();
      if (e.target && e.target.classList && e.target.classList.contains('poe-input')) {
        numpadFlyout.open(e, e.target);
      }
    });

    // Results Sort Dropdown
    var resultsSort = document.getElementById('resultsSort');
    if (resultsSort) {
      resultsSort.addEventListener('change', function() {
        var key = itemController ? itemController.currentPresetKey : 'chambers';
        tradeController.renderSearchResults(key);
      });
    }
  }

  window.openItemPreviewModalFromBackend = function(item) {
    var modal = document.getElementById('itemPreviewModal');
    var titleEl = document.getElementById('previewModalTitle');
    var bodyEl = document.getElementById('previewModalBody');
    if (!modal || !bodyEl) return;

    titleEl.innerText = item.itemName || item.itemBaseType || 'Item Preview';
    bodyEl.innerHTML = '';

    var headerParts = [];
    if (item.itemBaseType && item.itemBaseType !== item.itemName) headerParts.push(item.itemBaseType);
    if (item.itemLevel) headerParts.push('iLvl ' + item.itemLevel);
    if (item.socketsSummary) headerParts.push(item.socketsSummary);

    if (headerParts.length > 0) {
      var subHead = document.createElement('div');
      subHead.style.color = 'var(--poe-gold)';
      subHead.style.fontWeight = '700';
      subHead.style.marginBottom = '4px';
      subHead.innerText = headerParts.join(' • ');
      bodyEl.appendChild(subHead);
    }

    if (item.totalDps > 0) {
      var dpsLine = document.createElement('div');
      dpsLine.style.color = '#fbbf24';
      dpsLine.style.fontSize = '10.5px';
      dpsLine.style.fontWeight = '700';
      dpsLine.style.marginBottom = '4px';
      var dpsParts = ['Total DPS: ' + item.totalDps];
      if (item.physicalDps > 0) dpsParts.push('pDPS: ' + item.physicalDps);
      if (item.elementalDps > 0) dpsParts.push('eDPS: ' + item.elementalDps);
      dpsLine.innerText = dpsParts.join(' | ');
      bodyEl.appendChild(dpsLine);
    }

    if (Array.isArray(item.properties) && item.properties.length > 0) {
      var pSec = document.createElement('div');
      pSec.style.borderBottom = '1px solid #232734';
      pSec.style.paddingBottom = '4px';
      pSec.style.marginBottom = '4px';
      item.properties.forEach(function(p) {
        var r = document.createElement('div');
        r.style.color = '#e2e5ee';
        if (typeof p === 'object' && p !== null) {
          r.innerText = (p.label ? p.label + ': ' : '') + (p.val || '');
        } else {
          r.innerText = String(p);
        }
        pSec.appendChild(r);
      });
      bodyEl.appendChild(pSec);
    }

    if (Array.isArray(item.requirements) && item.requirements.length > 0) {
      var reqSec = document.createElement('div');
      reqSec.style.borderBottom = '1px solid #232734';
      reqSec.style.paddingBottom = '4px';
      reqSec.style.marginBottom = '4px';
      item.requirements.forEach(function(req) {
        var r = document.createElement('div');
        r.style.color = '#838a9a';
        r.innerText = String(req);
        reqSec.appendChild(r);
      });
      bodyEl.appendChild(reqSec);
    }

    var hasAnyMod = false;
    var structuredMods = [];

    if (Array.isArray(item.modifiers) && item.modifiers.length > 0) {
      item.modifiers.forEach(function(m) {
        var mType = (m.type || 'explicit').toLowerCase();
        var badge = m.tier || m.roll || (mType === 'implicit' ? 'IMP' : (mType === 'enchant' ? 'ENC' : (mType === 'fractured' ? 'FRAC' : (mType === 'crafted' ? 'CRAFT' : ''))));
        var text = (m.text || m.name || '') + (m.magnitudes ? ' ' + m.magnitudes : '');
        structuredMods.push({
          type: mType,
          badge: badge,
          text: text,
          isUnscalable: !!m.isUnscalable
        });
      });
    } else {
      if (Array.isArray(item.enchant)) {
        item.enchant.forEach(function(m) { structuredMods.push({ type: 'enchant', badge: 'ENC', text: String(m), isUnscalable: false }); });
      }
      if (Array.isArray(item.implicits)) {
        item.implicits.forEach(function(m) { structuredMods.push({ type: 'implicit', badge: 'IMP', text: String(m), isUnscalable: false }); });
      }
      if (Array.isArray(item.fractured)) {
        item.fractured.forEach(function(m) { structuredMods.push({ type: 'fractured', badge: 'FRAC', text: String(m), isUnscalable: false }); });
      }
      if (Array.isArray(item.explicits)) {
        item.explicits.forEach(function(m) { structuredMods.push({ type: 'explicit', badge: '', text: String(m), isUnscalable: false }); });
      }
      if (Array.isArray(item.crafted)) {
        item.crafted.forEach(function(m) { structuredMods.push({ type: 'crafted', badge: 'CRAFT', text: String(m), isUnscalable: false }); });
      }
    }

    if (structuredMods.length > 0) {
      hasAnyMod = true;
      var modSec = document.createElement('div');
      modSec.style.display = 'flex';
      modSec.style.flexDirection = 'column';
      modSec.style.gap = '3px';

      var sawTopMod = false;
      var insertedDivider = false;

      structuredMods.forEach(function(m) {
        var isTop = (m.type === 'implicit' || m.type === 'enchant');
        if (isTop) {
          sawTopMod = true;
        } else if (sawTopMod && !insertedDivider) {
          var div = document.createElement('div');
          div.className = 'mod-implicit-divider';
          modSec.appendChild(div);
          insertedDivider = true;
        }

        var row = document.createElement('div');
        row.className = 'stat-line is-selected mod-type-' + m.type;
        row.style.marginBottom = '1px';

        var wrap = document.createElement('div');
        wrap.className = 'stat-label-wrap';
        wrap.style.cursor = 'default';

        var badgeHtml = m.badge ? '<span class="roll-quality mod-type-' + m.type + '">' + m.badge + '</span> ' : '';
        var unscalableHtml = m.isUnscalable ? ' <span class="unscalable-badge">[Unscalable]</span>' : '';
        wrap.innerHTML = badgeHtml + '<span class="stat-text">' + m.text + '</span>' + unscalableHtml;

        row.appendChild(wrap);
        modSec.appendChild(row);
      });

      bodyEl.appendChild(modSec);
    }

    if (item.isCorrupted) {
      var corrLine = document.createElement('div');
      corrLine.style.color = '#ef4444';
      corrLine.style.fontWeight = '700';
      corrLine.style.marginTop = '4px';
      corrLine.innerText = 'CORRUPTED';
      bodyEl.appendChild(corrLine);
    }

    if (!hasAnyMod && (!Array.isArray(item.properties) || item.properties.length === 0)) {
      var empty = document.createElement('div');
      empty.style.color = '#64748b';
      empty.innerText = 'No extra modifiers on this item.';
      bodyEl.appendChild(empty);
    }

    modal.style.display = 'flex';
  };

  window.closeItemPreviewModal = function() {
    var modal = document.getElementById('itemPreviewModal');
    if (modal) modal.style.display = 'none';
  };

  // DOM Ready
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initApp);
  } else {
    initApp();
  }
})();
