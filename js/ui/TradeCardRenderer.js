// Trade Card DOM Renderer Constructor
function TradeCardRenderer() {}

TradeCardRenderer.prototype.createCardElement = function(item, preset) {
  preset = preset || {};
  var card = document.createElement('div');
  card.className = 'trade-card ' + (item.priceType === 'Divine' ? 'trade-card-divine' : 'trade-card-chaos');

  var leftCol = document.createElement('div');
  leftCol.className = 'trade-left-column';

  var titleRow = document.createElement('div');
  titleRow.className = 'item-title-row';

  var nameSpan = document.createElement('span');
  nameSpan.className = 'item-name-title';
  nameSpan.innerText = item.itemName || preset.name || 'Item';
  if (preset.nameColor) {
    nameSpan.style.color = preset.nameColor;
  }
  titleRow.appendChild(nameSpan);

  if (Array.isArray(item.highlights)) {
    item.highlights.forEach(function(h) {
      var badge = document.createElement('span');
      badge.className = 'trade-highlight-badge';
      badge.innerText = h;
      titleRow.appendChild(badge);
    });
  }

  var sellerInfo = document.createElement('div');
  sellerInfo.className = 'trade-seller-info';

  var statusDot = document.createElement('span');
  statusDot.className = 'status-dot ' + (item.status || 'online');
  sellerInfo.appendChild(statusDot);

  var sellerIdent = document.createElement('div');
  sellerIdent.className = 'seller-identity';

  var safeSeller = item.seller || item.account || 'Exile';
  var safeChar = item.character || item.char || item.ign || (safeSeller !== 'Exile' ? safeSeller : 'Unknown');

  var sellerName = document.createElement('span');
  sellerName.className = 'seller-name';
  sellerName.innerText = '@' + safeSeller;
  sellerIdent.appendChild(sellerName);

  var sellerChar = document.createElement('span');
  sellerChar.className = 'seller-char';
  sellerChar.innerText = 'ign: ' + safeChar;
  sellerIdent.appendChild(sellerChar);

  sellerInfo.appendChild(sellerIdent);

  var listingAge = document.createElement('span');
  listingAge.className = 'listing-age';
  listingAge.innerText = '• ' + (item.age || 'RECENT');
  sellerInfo.appendChild(listingAge);

  leftCol.appendChild(titleRow);
  leftCol.appendChild(sellerInfo);

  var rightGroup = document.createElement('div');
  rightGroup.className = 'trade-right-group';

  var priceTag = document.createElement('div');
  priceTag.className = 'trade-price-tag';

  var priceAmt = document.createElement('span');
  priceAmt.className = 'price-amount';
  priceAmt.innerText = item.priceVal;
  priceTag.appendChild(priceAmt);

  var currBadge = document.createElement('span');
  currBadge.className = 'currency-badge ' + (item.priceType === 'Divine' ? 'currency-divine' : 'currency-chaos');
  currBadge.innerText = item.priceType;
  priceTag.appendChild(currBadge);

  var actions = document.createElement('div');
  actions.className = 'trade-actions';

  var hideoutBtn = document.createElement('button');
  hideoutBtn.type = 'button';
  hideoutBtn.className = 'hideout-btn';
  hideoutBtn.setAttribute('data-action', 'to-hideout');
  hideoutBtn.setAttribute('data-seller', safeSeller);
  if (item.hideoutToken) {
    hideoutBtn.setAttribute('data-token', item.hideoutToken);
  }

  var hideoutText = document.createElement('span');
  hideoutText.innerText = 'To hideout';
  hideoutBtn.appendChild(hideoutText);
  if (item.goldVal && Number(item.goldVal) > 0) {
    var goldVal = document.createElement('span');
    goldVal.className = 'btn-gold-val';
    goldVal.innerText = '(' + item.goldVal + ')';
    hideoutBtn.appendChild(goldVal);
  }

  var previewBtn = document.createElement('button');
  previewBtn.type = 'button';
  previewBtn.className = 'whisper-btn';
  previewBtn.setAttribute('data-action', 'preview-item');
  previewBtn.setAttribute('data-item-json', encodeURIComponent(JSON.stringify(item)));
  previewBtn.innerText = 'Preview';

  actions.appendChild(hideoutBtn);
  actions.appendChild(previewBtn);

  rightGroup.appendChild(priceTag);
  rightGroup.appendChild(actions);

  card.appendChild(leftCol);
  card.appendChild(rightGroup);

  return card;
};
