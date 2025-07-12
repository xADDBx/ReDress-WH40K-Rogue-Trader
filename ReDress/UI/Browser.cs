using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static UnityEngine.GUILayout;
using UnityEngine;
using Owlcat.Runtime.Core;

namespace ReDress {
    public class Browser {
        public enum sortDirection {
            Ascending = 1,
            Descending = -1
        };
    }
    public class Browser<Definition, Item> : Browser {
        private IEnumerable<Definition> _pagedResults = new List<Definition>();
        private Queue<Definition> cachedSearchResults;
        public List<Definition> filteredDefinitions = new();
        public List<Definition> tempFilteredDefinitions;
        private Dictionary<Definition, Item> _currentDict;
        private CancellationTokenSource _searchCancellationTokenSource;
        private string _searchText = "";
        public string SearchText => _searchText;
        public sortDirection SortDirection = sortDirection.Ascending;
        public bool SearchAsYouType = true;
        public bool _doCopyToEnd = false;
        public bool _finishedCopyToEnd = false;

        public int SearchLimit {
            get => 30;
            set {
                SearchLimit = value;
            }
        }
        private int _pageCount;
        private int _matchCount;
        private int _currentPage = 1;
        public bool searchQueryChanged = true;
        public void ResetSearch() {
            searchQueryChanged = true;
            ReloadData();
        }
        public bool needsReloadData = true;
        public void ReloadData() => needsReloadData = true;
        private bool _updatePages = false;
        private bool _finishedSearch = false;
        public bool isSearching = false;
        public Browser(bool searchAsYouType) {
            SearchAsYouType = searchAsYouType;
        }

        public void OnGUI(
            IEnumerable<Item> current,
            Func<Item, Definition> definition,
            Func<Definition, string> searchKey,
            Func<Definition, IComparable[]> sortKeys,
            Action<Definition, Item> onRowGUI = null) {
            current ??= new List<Item>();
            List<Definition> definitions = Update(current, searchKey, sortKeys, definition);
            using (new HorizontalScope()) {
                Space(50);
                Label("Limit", ExpandWidth(false));
                var newSearchText = TextField(String.Copy(_searchText), Width(400));
                if (newSearchText != _searchText) {
                    _searchText = newSearchText;
                    ResetSearch();
                }
                Space(25);
                if (_doCopyToEnd) {
                    Label("Copying...", ExpandWidth(false));
                    Space(25);
                }
            }
            using (new HorizontalScope()) {
                Space(50);
                if (_matchCount > 0 || _searchText.Length > 0) {
                    string matchesText = "Matches: " + $"{_matchCount}";
                    if (_matchCount > SearchLimit) { matchesText += " => " + $"{SearchLimit}"; }

                    Label(matchesText, ExpandWidth(false));
                }
                if (_matchCount > SearchLimit) {
                    string pageLabel = "Page: " + _currentPage.ToString() + " / " + _pageCount.ToString();
                    Space(25);
                    Label(pageLabel, ExpandWidth(false));
                    if (Button("-", ExpandWidth(false))) {
                        if (_currentPage == 1) {
                            _currentPage = _pageCount;
                        } else {
                            _currentPage -= 1;
                        }
                        _updatePages = true;
                    }
                    if (Button("+", ExpandWidth(false))) {
                        if (_currentPage == _pageCount) {
                            _currentPage = 1;
                        } else {
                            _currentPage += 1;
                        }
                        _updatePages = true;
                    }
                }
            }
            foreach (var def in definitions) {
                _currentDict.TryGetValue(def, out var item);
                if (onRowGUI != null) {
                    using (new HorizontalScope()) {
                        Space(50);
                        onRowGUI(def, item);
                    }
                }
            }
        }

        private List<Definition> Update(
            IEnumerable<Item> current,
            Func<Definition, string> searchKey,
            Func<Definition, IComparable[]> sortKeys,
            Func<Item, Definition> definition) {
            if (Event.current.type == EventType.Layout) {
                if (_finishedSearch || isSearching) {
                    bool nothingToSearch = current.Count() == 0;
                    // If the search has at least one result
                    if ((cachedSearchResults.Count > 0 || nothingToSearch) && (searchQueryChanged || _finishedSearch)) {
                        Comparer<Definition> comparer = Comparer<Definition>.Create((x, y) => {
                            var xKeys = sortKeys(x);
                            var yKeys = sortKeys(y);
                            var zipped = xKeys.Zip(yKeys, (x, y) => (x: x, y: y));
                            foreach (var pair in zipped) {
                                var compare = pair.x.CompareTo(pair.y);
                                if (compare != 0) return (int)SortDirection * compare;
                            }
                            return (int)SortDirection * (xKeys.Length > yKeys.Length ? -1 : 1);
                        });
                        if (_finishedSearch && !searchQueryChanged) {
                            filteredDefinitions = new List<Definition>();
                        }
                        if (_doCopyToEnd && _finishedCopyToEnd) {
                            _doCopyToEnd = false;
                            _finishedCopyToEnd = false;
                            cachedSearchResults.Clear();
                            filteredDefinitions = tempFilteredDefinitions;
                        } else {
                            // If the search already finished we want to copy all results as fast as possible
                            if (_finishedSearch && cachedSearchResults.Count < 1000) {
                                filteredDefinitions.AddRange(cachedSearchResults);
                                cachedSearchResults.Clear();
                                filteredDefinitions.Sort(comparer);
                            } // If it's too much then even the above approach will take up to ~10 seconds on decent setups
                            else if (_finishedSearch && !_doCopyToEnd) {
                                _doCopyToEnd = true;
                                _finishedCopyToEnd = false;
                                Task.Run(() => CopyToEnd(filteredDefinitions, cachedSearchResults, comparer));
                            } // If it's not finished then we shouldn't have too many results anyway
                            else if (!_doCopyToEnd) {
                                // Lock the search results
                                lock (cachedSearchResults) {
                                    // Go through every item in the queue
                                    while (cachedSearchResults.Count > 0) {
                                        // Add the item into the OrderedSet filteredDefinitions
                                        filteredDefinitions.Add(cachedSearchResults.Dequeue());
                                    }
                                }
                                filteredDefinitions.Sort(comparer);
                            }
                        }
                    }
                    _matchCount = filteredDefinitions.Count;
                    UpdatePageCount();
                    UpdatePaginatedResults();
                    if (_finishedSearch && cachedSearchResults?.Count == 0) {
                        isSearching = false;
                        _updatePages = false;
                        _finishedSearch = false;
                        searchQueryChanged = false;
                        cachedSearchResults = null;
                    }
                }
                if (needsReloadData) {
                    _currentDict = new();
                    current.ForEach(i => {
                        if (!_currentDict.ContainsKey(definition(i))) {
                            _currentDict[definition(i)] = i;
                        }
                    });
                    IEnumerable<Definition> definitions = _currentDict.Keys.ToList();
                    if (!isSearching) {
                        _searchCancellationTokenSource = new();
                        Task.Run(() => UpdateSearchResults(_searchText, definitions, searchKey));
                        if (searchQueryChanged) {
                            filteredDefinitions = new List<Definition>();
                        }
                        isSearching = true;
                        needsReloadData = false;
                    } else {
                        _searchCancellationTokenSource.Cancel();
                    }
                }
                if (_updatePages) {
                    _updatePages = false;
                    UpdatePageCount();
                    UpdatePaginatedResults();
                }
            }
            return _pagedResults?.ToList();
        }

        public void CopyToEnd(List<Definition> filteredDefinitions, Queue<Definition> cachedSearchResults, Comparer<Definition> comparer) {
            tempFilteredDefinitions = filteredDefinitions.Concat(cachedSearchResults).ToList();
            if (_searchCancellationTokenSource?.IsCancellationRequested ?? false) {
                tempFilteredDefinitions.Clear();
            }
            tempFilteredDefinitions.Sort(comparer);
            _finishedCopyToEnd = true;
        }

        public void UpdateSearchResults(string searchTextParam,
            IEnumerable<Definition> definitions,
            Func<Definition, string> searchKey) {
            if (definitions == null) {
                return;
            }
            cachedSearchResults = new();
            var terms = searchTextParam.Split(' ').Select(s => s.ToLower()).ToHashSet();
            if (!string.IsNullOrEmpty(searchTextParam)) {
                foreach (var def in definitions) {
                    if (_searchCancellationTokenSource.IsCancellationRequested) {
                        isSearching = false;
                        return;
                    }
                    if (def.GetType().ToString().Contains(searchTextParam)
                       ) {
                        lock (cachedSearchResults) {
                            cachedSearchResults.Enqueue(def);
                        }
                    } else if (searchKey != null) {
                        var text = searchKey(def).ToLower();
                        if (terms.All(term => text.IndexOf(term, 0, StringComparison.InvariantCultureIgnoreCase) != -1)) {
                            lock (cachedSearchResults) {
                                cachedSearchResults.Enqueue(def);
                            }
                        }
                    }
                }
            } else {
                lock (cachedSearchResults) {
                    cachedSearchResults = new Queue<Definition>(definitions);
                }
            }
            _finishedSearch = true;
        }
        public void UpdatePageCount() {
            if (SearchLimit > 0) {
                _pageCount = (int)Math.Ceiling((double)_matchCount / SearchLimit);
                _currentPage = Math.Min(_currentPage, _pageCount);
                _currentPage = Math.Max(1, _currentPage);
            } else {
                _pageCount = 1;
                _currentPage = 1;
            }
        }
        public void UpdatePaginatedResults() {
            var limit = SearchLimit;
            var count = _matchCount;
            var offset = Math.Min(count, (_currentPage - 1) * limit);
            limit = Math.Min(limit, Math.Max(count, count - limit));
            _pagedResults = filteredDefinitions.Skip(offset).Take(limit).ToArray();
        }
    }
}