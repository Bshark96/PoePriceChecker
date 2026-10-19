// Live Search Controller Constructor
function LiveSearchController() {
  this.monitoredSearches = [
    new LiveSearchQueryModel('m1', 'CHAMBERS OF INSCRIPTION', 3.0, 'Divine', true),
    new LiveSearchQueryModel('m2', "DORYANI'S PROTOTYPE", 2.0, 'Divine', true)
  ];
  this.foundItems = [];
  this.maxHistoryItems = 10;
  this.isLiveActive = true;
  this.streamInterval = null;
}

LiveSearchController.prototype.init = function() {
  this.renderMonitoredSearches();
  this.startLiveStream();
};

LiveSearchController.prototype.initReal = function() {
  this.monitoredSearches = [];
  this.foundItems = [];
  var listEl = document.getElementById('liveResultsList');
  if (listEl) {
    listEl.innerHTML = '';
  }
  this.renderMonitoredSearches();
  // Do not start mock live stream interval in real Game Bar mode
};

LiveSearchController.prototype.renderMonitoredSearches = function() {
  var listEl = document.getElementById('monitoredChipsList');
  var badgeEl = document.getElementById('monitoredCountBadge');
  if (!listEl) return;

  var activeCount = 0;
  for (var i = 0; i < this.monitoredSearches.length; i++) {
    if (this.monitoredSearches[i].active) activeCount++;
  }
  if (badgeEl) badgeEl.innerText = activeCount + ' ACTIVE';

  listEl.innerHTML = '';
  if (this.monitoredSearches.length === 0) {
    var emptyDiv = document.createElement('div');
    emptyDiv.className = 'monitored-empty-text';
    emptyDiv.innerText = 'No items being monitored. Paste trade link or type item name above.';
    listEl.appendChild(emptyDiv);
    return;
  }

  var self = this;
  this.monitoredSearches.forEach(function(m) {
    var row = document.createElement('div');
    row.className = 'monitored-item-row' + (m.active ? '' : ' paused');

    var filterDesc = (m.maxPrice !== null && !isNaN(m.maxPrice)) ? ('≤ ' + m.maxPrice + ' ' + m.currency) : ('Any (' + m.currency + ')');

    row.innerHTML = 
      '<div class="monitored-item-left">' +
        '<input type="checkbox" class="monitored-checkbox" ' + (m.active ? 'checked' : '') + ' />' +
        '<div class="monitored-item-details">' +
          '<span class="monitored-item-name">' + m.queryText + '</span>' +
          '<span class="monitored-item-filter">' + filterDesc + '</span>' +
        '</div>' +
      '</div>' +
      '<div class="monitored-item-right">' +
        '<span class="monitored-status-tag ' + (m.active ? 'active' : 'paused') + '">' + (m.active ? 'LIVE' : 'PAUSED') + '</span>' +
        '<button type="button" class="monitored-del-btn" title="Remove monitored search" data-action="delete-live" data-id="' + m.id + '">×</button>' +
      '</div>';

    var cb = row.querySelector('.monitored-checkbox');
    if (cb) {
      cb.addEventListener('change', function() {
        self.toggleMonitoredSearch(m.id);
      });
    }

    listEl.appendChild(row);
  });
};

LiveSearchController.prototype.pasteFromClipboard = function() {
  var self = this;
  if (window.PoeBridge && window.PoeBridge.isGameBarHost()) {
    window.PoeBridge.notifyHost({ action: 'live_paste' });
    return;
  }

  if (navigator.clipboard && typeof navigator.clipboard.readText === 'function') {
    navigator.clipboard.readText().then(function(text) {
      self.onClipboardPasted(text);
    }).catch(function() {
      if (window.PoeBridge) {
        window.PoeBridge.showToast('Unable to read clipboard in browser preview.');
      }
    });
  } else if (window.PoeBridge) {
    window.PoeBridge.notifyHost({ action: 'live_paste' });
  }
};

LiveSearchController.prototype.onClipboardPasted = function(text) {
  var inputEl = document.getElementById('liveSearchInput');
  var trimmed = (text || '').trim();
  if (!trimmed) {
    if (window.PoeBridge) {
      window.PoeBridge.showToast('Clipboard is empty.');
    }
    return;
  }

  if (inputEl) {
    inputEl.value = trimmed;
    inputEl.focus();
  }

  if (window.PoeBridge) {
    window.PoeBridge.showToast('Pasted link from clipboard');
  }
};

LiveSearchController.prototype.addNewLiveSearch = function() {
  var inputEl = document.getElementById('liveSearchInput');
  var priceEl = document.getElementById('liveMaxPriceInput');
  var currencyEl = document.getElementById('liveCurrencySelect');

  var rawInput = inputEl ? inputEl.value.trim() : '';
  if (!rawInput) {
    if (window.PoeBridge) window.PoeBridge.showToast('Please paste a trade link or enter an item name.');
    return;
  }

  var priceVal = priceEl && priceEl.value !== '' ? priceEl.value.trim() : '';
  var currencyVal = currencyEl ? currencyEl.value : 'Chaos';

  var displayLabel = rawInput.toUpperCase();
  var urlMatch = rawInput.match(/pathofexile\.com\/trade\/(?:search|live)\/([^\/]+)\/([^\/?#\s]+)/i);
  if (urlMatch && urlMatch[2]) {
    var leaguePart = decodeURIComponent(urlMatch[1] || '');
    var idPart = urlMatch[2].replace(/\/?live$/i, '');
    var shortId = idPart.length > 10 ? (idPart.substring(0, 10) + '...') : idPart;
    displayLabel = 'TRADE #' + shortId + (leaguePart ? (' (' + leaguePart.toUpperCase() + ')') : '');
  }

  var model = new LiveSearchQueryModel('m_' + Date.now(), displayLabel, priceVal, currencyVal, true);
  model.rawUrl = rawInput;
  this.monitoredSearches.push(model);

  if (window.PoeBridge) {
    window.PoeBridge.notifyHost({
      action: 'live_add',
      id: model.id,
      name: model.queryText,
      url: rawInput,
      maxPrice: model.maxPrice,
      currency: model.currency
    });
  }

  if (inputEl) inputEl.value = '';
  if (priceEl) priceEl.value = '';

  this.renderMonitoredSearches();
  if (window.PoeBridge) {
    window.PoeBridge.showToast('Added ' + displayLabel + ' to live search monitoring');
  }
};

LiveSearchController.prototype.toggleMonitoredSearch = function(id) {
  for (var i = 0; i < this.monitoredSearches.length; i++) {
    if (this.monitoredSearches[i].id === id) {
      var item = this.monitoredSearches[i];
      item.toggle();
      if (window.PoeBridge) {
        if (item.active) {
          window.PoeBridge.notifyHost({
            action: 'live_add',
            id: item.id,
            name: item.queryText,
            url: item.rawUrl || item.queryText,
            maxPrice: item.maxPrice,
            currency: item.currency
          });
        } else {
          window.PoeBridge.notifyHost({ action: 'live_remove', id: item.id, name: item.queryText });
        }
      }
      break;
    }
  }
  this.renderMonitoredSearches();
};

LiveSearchController.prototype.removeMonitoredSearch = function(id) {
  var removed = null;
  this.monitoredSearches = this.monitoredSearches.filter(function(m) {
    if (m.id === id) {
      removed = m;
      return false;
    }
    return true;
  });

  if (removed && window.PoeBridge) {
    window.PoeBridge.notifyHost({ action: 'live_remove', id: id, name: removed.queryText });
    window.PoeBridge.showToast('Removed ' + removed.queryText + ' from monitoring.');
  }

  this.renderMonitoredSearches();
};

LiveSearchController.prototype.clearHistory = function(fromHost) {
  this.foundItems = [];
  var listEl = document.getElementById('liveResultsList');
  if (listEl) {
    listEl.innerHTML = '';
  }

  if (!fromHost && window.PoeBridge) {
    window.PoeBridge.notifyHost({ action: 'live_clear_history' });
    window.PoeBridge.showToast('Live search history cleared');
  }
};

LiveSearchController.prototype.startLiveStream = function() {
  var self = this;
  if (this.streamInterval) clearInterval(this.streamInterval);

  var mockFeed = [
    { name: 'CHAMBERS OF INSCRIPTION', seller: 'GodSpeedTrader', char: 'FastExile', price: '2.4', curr: 'Divine', age: 'JUST NOW', gold: 450 },
    { name: "DORYANI'S PROTOTYPE", seller: 'ShockVaal', char: 'ElectroBlast', price: '1.8', curr: 'Divine', age: '10s AGO', gold: 450 },
    { name: 'CHAMBERS OF INSCRIPTION', seller: 'TempleRunner', char: 'VaultLover', price: '2.5', curr: 'Divine', age: '25s AGO', gold: 450 }
  ];

  var feedIdx = 0;
  this.streamInterval = setInterval(function() {
    var activeList = self.monitoredSearches.filter(function(m) { return m.active; });
    if (activeList.length === 0) return;

    var candidate = mockFeed[feedIdx % mockFeed.length];
    feedIdx++;

    self.pushLiveListing(candidate);
  }, 12000);
};

LiveSearchController.prototype.pushLiveListing = function(data) {
  var listEl = document.getElementById('liveResultsList');
  if (!listEl || !data) return;

  this.foundItems.unshift(data);
  if (this.foundItems.length > this.maxHistoryItems) {
    this.foundItems.length = this.maxHistoryItems;
  }

  var card = document.createElement('div');
  card.className = 'trade-card trade-card-divine';

  var goldNum = typeof data.gold === 'number' ? data.gold : parseInt(data.gold, 10);
  var goldHtml = (!isNaN(goldNum) && goldNum > 0) ? '<span class="btn-gold-val">(' + goldNum + ')</span>' : '';
  var tokenAttr = data.hideoutToken ? (' data-token="' + data.hideoutToken + '"') : '';

  card.innerHTML = 
    '<div class="trade-left-column">' +
      '<div class="item-title-row trade-title-gold">' + data.name + '</div>' +
      '<div class="trade-seller-info">' +
        '<span class="status-dot online"></span>' +
        '<div class="seller-identity">' +
          '<span class="seller-name">@' + data.seller + '</span>' +
          '<span class="seller-char">ign: ' + data.char + '</span>' +
        '</div>' +
        '<span class="listing-age">• ' + data.age + '</span>' +
      '</div>' +
    '</div>' +
    '<div class="trade-right-group">' +
      '<div class="trade-price-tag">' +
        '<span class="price-amount">' + data.price + '</span>' +
        '<span class="currency-badge currency-divine">' + data.curr + '</span>' +
      '</div>' +
      '<div class="trade-actions">' +
        '<button type="button" class="hideout-btn" data-action="to-hideout" data-seller="' + data.seller + '"' + tokenAttr + '>' +
          '<span>To hideout</span>' +
          goldHtml +
        '</button>' +
        '<button type="button" class="whisper-btn" data-action="whisper" data-seller="' + data.seller + '" data-char="' + data.char + '" data-price="' + data.price + '" data-currency="' + data.curr + '">Whisper</button>' +
      '</div>' +
    '</div>';

  listEl.insertBefore(card, listEl.firstChild);
  while (listEl.children.length > this.maxHistoryItems) {
    listEl.removeChild(listEl.lastChild);
  }
};
