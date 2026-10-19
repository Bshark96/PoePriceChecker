// WinUI 2 WebView2 Web-to-C# Bridge
var PoeBridge = {
  isGameBarHost: function() {
    return !!(window.chrome && window.chrome.webview);
  },

  notifyHost: function(data) {
    var payload = (typeof data === 'string') ? data : JSON.stringify(data);
    var sentNative = false;

    // Method 1: Modern WebView2 postMessage (Direct, instant, zero popups)
    try {
      if (window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function') {
        window.chrome.webview.postMessage(payload);
        sentNative = true;
      }
    } catch (e1) {
      // Safe catch for WebView2 postMessage
    }

    // Method 2: Fallback for Web Browser Preview / Standalone iFrame
    if (!sentNative) {
      this.handleWebPreviewFallback(data);
    }
  },

  handleWebPreviewFallback: function(data) {
    if (!data) return;
    var obj = (typeof data === 'string') ? null : data;
    if (!obj) {
      try { obj = JSON.parse(data); } catch (e) { obj = {}; }
    }

    var action = obj.action || '';

    // Handle Copy to Clipboard in Browser Preview
    if (obj.command) {
      this.copyToClipboard(obj.command);
    } else if (obj.text) {
      this.copyToClipboard(obj.text);
    }

    // Handle Actions in Web Preview
    switch (action.toLowerCase()) {
      case 'dismiss':
      case 'minimize':
        if (window.viewController) {
          window.viewController.switchView('evaluate');
        }
        break;

      case 'search':
        if (window.tradeController) {
          window.tradeController.renderSearchResults(obj.preset || 'chambers');
        }
        break;

      case 'hideout':
        this.showToast('Copied to clipboard: ' + (obj.command || ('/hideout ' + obj.seller)));
        break;

      case 'whisper':
        this.showToast('Whisper copied: @' + obj.seller + ' (' + (obj.price || '') + ' ' + (obj.currency || '') + ')');
        break;
    }
  },

  copyToClipboard: function(text) {
    if (!text) return;
    try {
      if (navigator.clipboard && typeof navigator.clipboard.writeText === 'function') {
        navigator.clipboard.writeText(text);
      } else {
        var textArea = document.createElement('textarea');
        textArea.value = text;
        textArea.style.position = 'fixed';
        textArea.style.left = '-9999px';
        document.body.appendChild(textArea);
        textArea.select();
        document.execCommand('copy');
        document.body.removeChild(textArea);
      }
    } catch (err) {
      // Clipboard fallback
    }
  },

  minimizeWidget: function() {
    this.notifyHost({ action: 'minimize' });
    this.notifyHost({ action: 'dismiss' });
  },

  showToast: function(msg) {
    var toast = document.getElementById('toastNotification');
    if (!toast) return;
    toast.innerText = msg;
    toast.classList.add('show');
    setTimeout(function() {
      toast.classList.remove('show');
    }, 2500);
  },

  onItemLoaded: function(item) {
    if (!item) return;
    if (window.viewController && window.viewController.getCurrentView() !== 'evaluate') {
      window.viewController.switchView('evaluate');
    }
    if (window.itemController) {
      window.itemController.loadCustomItem(item);
    }
    PoeBridge.showToast('Loaded PoE 1 Item: ' + (item.name || item.base));
  },

  onTradeResultsLoaded: function(data) {
    if (!data || !window.tradeController) return;
    window.tradeController.renderTradeResultsData(data);
  },

  onNinjaPriceUpdated: function(data) {
    if (!data) return;
    var ninjaPriceEl = document.getElementById('ninjaPrice');
    if (ninjaPriceEl && data.ninjaPrice) {
      ninjaPriceEl.innerText = data.ninjaPrice;
    }
    if (data.dustValue) {
      var dustEl = document.querySelector('.disenchant-label');
      if (dustEl) {
        dustEl.innerText = data.dustValue;
      }
    }
  },

  onLiveSearchResult: function(listing) {
    if (!listing) return;
    if (window.liveSearchController) {
      window.liveSearchController.pushLiveListing(listing);
    }
    var wrapper = document.getElementById('overlayWrapper');
    var isWebMinimized = !!(wrapper && wrapper.classList.contains('is-minimized'));
    if ((listing.openAlarm === true || isWebMinimized) && window.alarmController) {
      window.alarmController.triggerLiveAlert(listing);
    } else if (window.alarmController && !listing.soundPlayedByHost && typeof window.alarmController.playAlertSound === 'function') {
      window.alarmController.playAlertSound();
    }
  },

  onLiveSearchClipboardPasted: function(text) {
    if (window.liveSearchController) {
      window.liveSearchController.onClipboardPasted(text);
    }
  },

  onLiveSearchHistoryCleared: function() {
    if (window.liveSearchController) {
      window.liveSearchController.clearHistory(true);
    }
  },

  onRateLimitUpdated: function(statusText, isThrottled) {
    var rateLimitBadge = document.querySelector('.rate-limit-badge');
    if (rateLimitBadge) {
      var isLimited = isThrottled || (statusText && (statusText.indexOf('LIMITED') !== -1 || statusText.indexOf('WAIT') !== -1 || statusText.indexOf('429') !== -1));
      rateLimitBadge.innerText = 'RATE LIMIT: ' + (isLimited ? 'LIMITED' : (statusText || 'NORMAL'));
      if (isLimited) {
        rateLimitBadge.classList.add('is-limited');
      } else {
        rateLimitBadge.classList.remove('is-limited');
      }
    }
  },

  hostTimerPaused: false,

  onTimerTick: function(remainingSeconds, totalDurationSeconds, isPaused) {
    this.hostTimerPaused = !!isPaused;
    var timerEl = document.getElementById('autoMinimizeTimer');
    var autoTextSpan = document.getElementById('autoMinimizeText');
    var progressBarEl = document.getElementById('bottomProgressBar') || document.querySelector('.bottom-progress-bar');

    var rem = (typeof remainingSeconds === 'number') ? remainingSeconds : 0;
    var tot = (typeof totalDurationSeconds === 'number' && totalDurationSeconds > 0) ? totalDurationSeconds : 12;

    if (timerEl) {
      timerEl.innerText = rem.toFixed(1);
    }

    if (autoTextSpan) {
      if (isPaused) {
        autoTextSpan.innerHTML = 'Auto-minimizing in ' + rem.toFixed(1) + 's (Hovered - Paused)';
      } else {
        autoTextSpan.innerHTML = 'Auto-minimizing in <span id="autoMinimizeTimer">' + rem.toFixed(1) + '</span>s (Hover to pause)';
      }
    }

    if (progressBarEl) {
      var pct = Math.min(100, Math.max(0, (rem / tot) * 100));
      progressBarEl.style.width = pct + '%';
    }
  }
};

// Global Exposure
window.PoeBridge = PoeBridge;
window.notifyHost = function(data) { PoeBridge.notifyHost(data); };
window.showToast = function(msg) { PoeBridge.showToast(msg); };

// Compatibility Hooks for C# ExecuteScriptAsync calls
window.loadPoe1Item = function(item) {
  PoeBridge.onItemLoaded(item);
};

window.loadPoe1TradeResults = function(data) {
  PoeBridge.onTradeResultsLoaded(data);
};
