// Trade Controller Constructor
function TradeController(tradeCardRenderer) {
  this.cardRenderer = tradeCardRenderer || new TradeCardRenderer();
  this.activeSearchUrl = null;
}

TradeController.prototype.triggerSearch = function() {
  var isGameBar = window.PoeBridge && window.PoeBridge.isGameBarHost();
  var presetKey = window.itemController ? window.itemController.currentPresetKey : 'chambers';

  // Collect active modifier filters and min/max bounds
  var modLines = document.querySelectorAll('#modifierContainer .stat-line');
  var modFilters = [];
  for (var i = 0; i < modLines.length; i++) {
    var line = modLines[i];
    var isSel = line.classList.contains('is-selected');
    var minIn = line.querySelector('input.poe-input[placeholder="min"]');
    var maxIn = line.querySelector('input.poe-input[placeholder="max"]');
    var minV = minIn && minIn.value.trim() !== '' ? minIn.value.trim() : null;
    var maxV = maxIn && maxIn.value.trim() !== '' ? maxIn.value.trim() : null;
    modFilters.push({
      index: i,
      active: isSel,
      min: minV,
      max: maxV
    });
  }

  // Collect active property filters
  var propLines = document.querySelectorAll('#propertyContainer .property-line');
  var propFilters = [];
  for (var j = 0; j < propLines.length; j++) {
    var pLine = propLines[j];
    var pSel = pLine.classList.contains('is-selected');
    var pLabelEl = pLine.querySelector('.prop-label-wrap span');
    var pLabel = pLabelEl ? pLabelEl.innerText.replace(':', '').trim() : '';
    var pMinIn = pLine.querySelector('input.poe-input[placeholder="min"]');
    var pMaxIn = pLine.querySelector('input.poe-input[placeholder="max"]');
    var pMinV = pMinIn && pMinIn.value.trim() !== '' ? pMinIn.value.trim() : null;
    var pMaxV = pMaxIn && pMaxIn.value.trim() !== '' ? pMaxIn.value.trim() : null;
    propFilters.push({
      index: j,
      label: pLabel,
      active: pSel,
      min: pMinV,
      max: pMaxV
    });
  }

  if (window.PoeBridge) {
    window.PoeBridge.notifyHost({
      action: 'search',
      preset: presetKey,
      filters: {
        modifiers: modFilters,
        properties: propFilters
      }
    });
  }

  var loader = document.getElementById('searchLoadingBar');
  var listEl = document.getElementById('resultsList');
  if (loader) loader.classList.remove('is-hidden');
  if (listEl) listEl.style.opacity = '0.3';

  if (!isGameBar) {
    // Standalone browser preview: simulate mock response
    var self = this;
    setTimeout(function() {
      if (loader) loader.classList.add('is-hidden');
      if (listEl) listEl.style.opacity = '1';
      self.renderSearchResults(presetKey);
    }, 250);
  }
};

TradeController.prototype.renderSearchResults = function(key) {
  var listEl = document.getElementById('resultsList');
  if (!listEl) return;

  var rawList = (marketListings[key] || []).slice();
  var sortSelect = document.getElementById('resultsSort');
  var sortMode = (sortSelect && sortSelect.value) ? sortSelect.value : 'price-asc';

  var listings = rawList.map(function(item) {
    return new TradeListingModel(item);
  });

  if (sortMode === 'price-asc') {
    listings.sort(function(a, b) {
      return a.getNormalizedPriceInChaos() - b.getNormalizedPriceInChaos();
    });
  } else if (sortMode === 'price-desc') {
    listings.sort(function(a, b) {
      return b.getNormalizedPriceInChaos() - a.getNormalizedPriceInChaos();
    });
  }

  var countEl = document.getElementById('resultsCount');
  if (countEl) countEl.innerText = listings.length + ' LISTINGS';

  var preset = dataPresets[key] || dataPresets.chambers;
  listEl.innerHTML = '';

  var self = this;
  listings.forEach(function(listing) {
    var card = self.cardRenderer.createCardElement(listing, preset);
    listEl.appendChild(card);
  });
};

TradeController.prototype.renderTradeResultsData = function(data) {
  var loader = document.getElementById('searchLoadingBar');
  var listEl = document.getElementById('resultsList');
  var countEl = document.getElementById('resultsCount');

  if (loader) loader.classList.add('is-hidden');
  if (listEl) listEl.style.opacity = '1';

  if (countEl && data.totalCount !== undefined) {
    countEl.innerText = data.totalCount + ' LISTINGS';
  }
  if (!listEl || !Array.isArray(data.listings)) return;

  listEl.innerHTML = '';
  if (data.listings.length === 0) {
    listEl.innerHTML = '<div style="color: #94a3b8; font-size: 11px; padding: 24px; text-align: center;">No active listings found on official trade for these filters.</div>';
    return;
  }

  var self = this;
  data.listings.forEach(function(item) {
    var listing = new TradeListingModel(item);
    var card = self.cardRenderer.createCardElement(listing, { name: listing.itemName, nameColor: '#f7d070' });
    listEl.appendChild(card);
  });
};

TradeController.prototype.toHideout = function(seller, token) {
  if (!token) {
    if (window.PoeBridge && window.PoeBridge.showToast) {
      window.PoeBridge.showToast('No hideout token available for this listing.');
    }
    return;
  }
  if (window.PoeBridge) {
    window.PoeBridge.notifyHost({ 
      action: 'hideout', 
      seller: seller, 
      token: token 
    });
    window.PoeBridge.showToast('Teleporting to hideout...');
  }
};

TradeController.prototype.whisperSeller = function(seller, char, priceVal, priceType) {
  var currentItem = window.itemController ? window.itemController.currentItem : null;
  var p = dataPresets[window.itemController ? window.itemController.currentPresetKey : 'chambers'];
  var itemStr = (currentItem && currentItem.name) ? currentItem.name : ((p && p.name) ? p.name : 'item');
  var safeSeller = seller || 'Exile';
  var targetRecipient = (char && char !== 'Unknown') ? char : safeSeller;
  var whisperText = '@' + targetRecipient + ' Hi, I would like to buy your ' + itemStr + ' listed for ' + priceVal + ' ' + priceType + ' (stash tab "~b/o ' + priceVal + ' ' + priceType + '")';

  if (window.PoeBridge) {
    window.PoeBridge.notifyHost({
      action: 'whisper',
      seller: safeSeller,
      character: targetRecipient,
      text: whisperText,
      price: priceVal,
      currency: priceType
    });
    window.PoeBridge.showToast('Whisper copied: @' + targetRecipient + ' (' + priceVal + ' ' + priceType + ')');
  }
};
