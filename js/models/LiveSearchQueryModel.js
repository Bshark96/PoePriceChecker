// Path of Exile 1 Live Search Query Model Constructor
function LiveSearchQueryModel(id, queryText, maxPrice, currency, active) {
  this.id = id || ('lsq_' + Date.now() + '_' + Math.floor(Math.random() * 1000));
  this.queryText = queryText || '';
  this.maxPrice = maxPrice !== undefined && maxPrice !== '' ? parseFloat(maxPrice) : null;
  this.currency = currency || 'Divine';
  this.active = active !== undefined ? !!active : true;
  this.createdAt = new Date();
}

LiveSearchQueryModel.prototype.matches = function(listing) {
  if (!this.active) return false;
  if (!listing) return false;

  // Name match
  if (this.queryText) {
    var q = this.queryText.toLowerCase();
    var name = (listing.itemName || '').toLowerCase();
    if (name.indexOf(q) === -1) return false;
  }

  // Price match
  if (this.maxPrice !== null && !isNaN(this.maxPrice)) {
    var listingPrice = parseFloat(listing.priceVal) || 0;
    if (listing.priceType.toLowerCase() === this.currency.toLowerCase()) {
      if (listingPrice > this.maxPrice) return false;
    }
  }

  return true;
};

LiveSearchQueryModel.prototype.toggle = function() {
  this.active = !this.active;
  return this.active;
};
