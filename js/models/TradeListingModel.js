// Path of Exile 1 Trade Listing Model Constructor
function TradeListingModel(data) {
  data = data || {};
  this.seller = data.seller || data.account || 'Exile';
  this.character = data.character || data.char || data.ign || data.lastCharacterName || (this.seller !== 'Exile' ? this.seller : 'Unknown');
  this.priceVal = data.priceVal !== undefined ? String(data.priceVal) : (data.price !== undefined ? String(data.price) : '1');
  this.priceType = data.priceType || data.currency || data.curr || 'Divine';
  this.goldVal = data.goldVal !== undefined ? data.goldVal : (data.gold !== undefined ? data.gold : 450);
  this.status = data.status || 'online';
  this.age = data.age || 'JUST NOW';
  this.itemName = data.itemName || data.name || '';
  this.hideoutToken = data.hideoutToken || data.hideout_token || data.token || '';
  this.highlights = Array.isArray(data.highlights) ? data.highlights : [];
}

TradeListingModel.prototype.getNormalizedPriceInChaos = function(divineRatio) {
  divineRatio = divineRatio || 150;
  var numericVal = parseFloat(this.priceVal) || 0;
  if (this.priceType.toLowerCase() === 'divine') {
    return numericVal * divineRatio;
  }
  return numericVal;
};

TradeListingModel.prototype.getWhisperMessage = function(league) {
  league = league || 'Settlers';
  var itemStr = this.itemName || 'item';
  var target = (this.character && this.character !== 'Unknown') ? this.character : this.seller;
  return '@' + target + ' Hi, I would like to buy your ' + itemStr + ' listed for ' + this.priceVal + ' ' + this.priceType + ' in ' + league + ' (stash tab "~b/o ' + this.priceVal + ' ' + this.priceType + '")';
};

TradeListingModel.prototype.getHideoutCommand = function() {
  return '/hideout ' + this.seller;
};
