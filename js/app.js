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
    var overlayEl = document.getElementById('overlayWrapper') || document.body;
    var lastHoverState = false;

    function notifyHover(isHovered) {
      if (lastHoverState === isHovered) return;
      lastHoverState = isHovered;
      if (window.PoeBridge) {
        PoeBridge.notifyHost({ action: 'set_hover', isHovered: isHovered });
      }
    }

    overlayEl.addEventListener('mouseenter', function() { notifyHover(true); });
    overlayEl.addEventListener('mouseleave', function() { notifyHover(false); });
    overlayEl.addEventListener('pointerenter', function() { notifyHover(true); });
    overlayEl.addEventListener('pointerleave', function() { notifyHover(false); });
    document.addEventListener('mousemove', function() {
      notifyHover(true);
    }, { passive: true });

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
          case 'whisper':
            var wSeller = btn.getAttribute('data-seller');
            var wChar = btn.getAttribute('data-char');
            var wPrice = btn.getAttribute('data-price');
            var wCurr = btn.getAttribute('data-currency');
            tradeController.whisperSeller(wSeller, wChar, wPrice, wCurr);
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

    // Global CTRL+D shortcut to evaluate item
    window.addEventListener('keydown', function(e) {
      if ((e.ctrlKey || e.metaKey) && (e.key === 'd' || e.key === 'D' || e.keyCode === 68)) {
        e.preventDefault();
        if (window.PoeBridge) {
          window.PoeBridge.notifyHost({ action: 'evaluate' });
        }
        if (tradeController) {
          tradeController.triggerSearch();
        }
      }
    });

    // Contextmenu Delegation for PoE Inputs (Numpad Flyout)
    overlay.addEventListener('contextmenu', function(e) {
      if (e.target && e.target.classList.contains('poe-input')) {
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

  // DOM Ready
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initApp);
  } else {
    initApp();
  }
})();
