// Path of Exile 1 Trade Listing Model Constructor
function TradeListingModel(data) {
  data = data || {};
  this.id = data.id || '';
  this.queryId = data.queryId || '';
  this.seller = data.seller || data.account || 'Exile';
  this.character = data.character || data.char || data.ign || data.lastCharacterName || (this.seller !== 'Exile' ? this.seller : 'Unknown');
  this.priceVal = data.priceVal !== undefined ? String(data.priceVal) : (data.price !== undefined ? String(data.price) : '1');
  this.priceType = data.priceType || data.currency || data.curr || 'Divine';
  this.goldVal = data.goldVal !== undefined ? Number(data.goldVal) : (data.gold !== undefined ? Number(data.gold) : 0);
  this.status = data.status || 'online';
  this.age = data.age || 'JUST NOW';
  this.itemName = data.itemName || data.name || '';
  this.itemBaseType = data.itemBaseType || data.baseType || '';
  this.itemLevel = data.itemLevel || 0;
  this.isCorrupted = Boolean(data.isCorrupted);
  this.socketsSummary = data.socketsSummary || '';
  this.physicalDps = data.physicalDps || 0;
  this.elementalDps = data.elementalDps || 0;
  this.totalDps = data.totalDps || 0;
  this.properties = Array.isArray(data.properties) ? data.properties : [];
  this.requirements = Array.isArray(data.requirements) ? data.requirements : [];
  this.implicits = Array.isArray(data.implicits) ? data.implicits : [];
  this.explicits = Array.isArray(data.explicits) ? data.explicits : [];
  this.crafted = Array.isArray(data.crafted) ? data.crafted : [];
  this.fractured = Array.isArray(data.fractured) ? data.fractured : [];
  this.enchant = Array.isArray(data.enchant) ? data.enchant : [];
  this.modifiers = Array.isArray(data.modifiers) ? data.modifiers : [];
  this.flavourText = data.flavourText || '';
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
