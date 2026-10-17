// Live Search Controller Constructor
function LiveSearchController() {
  this.monitoredSearches = [
    new LiveSearchQueryModel('m1', 'CHAMBERS OF INSCRIPTION', 3.0, 'Divine', true),
    new LiveSearchQueryModel('m2', "DORYANI'S PROTOTYPE", 2.0, 'Divine', true)
  ];
  this.isLiveActive = true;
  this.streamInterval = null;
}

LiveSearchController.prototype.init = function() {
  this.renderMonitoredSearches();
  this.startLiveStream();
};

LiveSearchController.prototype.initReal = function() {
  this.monitoredSearches = [];
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
    emptyDiv.innerText = 'No items being monitored. Type item name above to add.';
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

LiveSearchController.prototype.addNewLiveSearch = function() {
  var inputEl = document.getElementById('liveSearchInput');
  var priceEl = document.getElementById('liveMaxPriceInput');
  var currencyEl = document.getElementById('liveCurrencySelect');

  var queryName = inputEl ? inputEl.value.trim() : '';
  if (!queryName) {
    if (window.PoeBridge) window.PoeBridge.showToast('Please enter an item name to add search.');
    return;
  }

  var priceVal = priceEl && priceEl.value !== '' ? priceEl.value.trim() : '';
  var currencyVal = currencyEl ? currencyEl.value : 'Chaos';

  var model = new LiveSearchQueryModel('m_' + Date.now(), queryName.toUpperCase(), priceVal, currencyVal, true);
  this.monitoredSearches.push(model);

  if (window.PoeBridge) {
    window.PoeBridge.notifyHost({
      action: 'live_add',
      name: model.queryText,
      maxPrice: model.maxPrice,
      currency: model.currency
    });
  }

  if (inputEl) inputEl.value = '';
  if (priceEl) priceEl.value = '';

  this.renderMonitoredSearches();
  if (window.PoeBridge) {
    window.PoeBridge.showToast('Added ' + queryName.toUpperCase() + ' to live search monitoring');
  }
};

LiveSearchController.prototype.toggleMonitoredSearch = function(id) {
  for (var i = 0; i < this.monitoredSearches.length; i++) {
    if (this.monitoredSearches[i].id === id) {
      this.monitoredSearches[i].toggle();
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
  if (!listEl) return;

  var card = document.createElement('div');
  card.className = 'trade-card trade-card-divine';

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
        '<button type="button" class="hideout-btn" data-action="to-hideout" data-seller="' + data.seller + '">' +
          '<span>To hideout</span>' +
          '<span class="btn-gold-val">(' + data.gold + ')</span>' +
        '</button>' +
        '<button type="button" class="whisper-btn" data-action="whisper" data-seller="' + data.seller + '" data-char="' + data.char + '" data-price="' + data.price + '" data-currency="' + data.curr + '">Whisper</button>' +
      '</div>' +
    '</div>';

  listEl.insertBefore(card, listEl.firstChild);
  if (listEl.children.length > 25) {
    listEl.removeChild(listEl.lastChild);
  }
};
