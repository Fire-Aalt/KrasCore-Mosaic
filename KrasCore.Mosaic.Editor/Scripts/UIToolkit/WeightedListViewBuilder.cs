using System;
using System.Collections.Generic;
using System.Linq;
using KrasCore.Mosaic.Authoring;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace KrasCore.Mosaic.Editor
{
    public class WeightedListViewBuilder<TObject, TEntry> 
        where TObject : Object
        where TEntry : new()
    {
        private const float BorderSize = 2;

        public VisualElement Root => _listView;

        private readonly ListView _listView;
        private readonly Func<TObject, TEntry> _convertFunc;
        private readonly List<TEntry> _boundList;
        
        public WeightedListViewBuilder(string name, string listLabel, RuleGroup dataSource,
            SerializedProperty serializedRule, string boundPropertyName,
            List<TEntry> boundList, Func<TObject, TEntry> convertFunc)
        {
            const int editorLineSize = 24;

            _boundList = boundList;
            _convertFunc = convertFunc;
            
            _listView = new ListView
            {
                name = name,
                reorderable = true,
                makeHeader = () =>
                {
                    var toolbar = new Toolbar();
                    toolbar.AddToClassList("list-view-header");
                    
                    var label = new Label { text = listLabel };
                    var spacer = new ToolbarSpacer();
                    var addBtn = new ToolbarButton(OnAddClicked) { text = "Add" };
                    var removeBtn = new ToolbarButton(OnRemoveClicked) { text = "Remove" };
                    
                    label.AddToClassList("list-view-header-label");
                    spacer.AddToClassList("list-view-header-spacer");
                    
                    toolbar.Add(label);
                    toolbar.Add(spacer);
                    toolbar.Add(addBtn);
                    toolbar.Add(removeBtn);
                    
                    return toolbar;
                },
                selectionType = SelectionType.Multiple,
                fixedItemHeight = editorLineSize * 2 + BorderSize * 2,
                dataSource = dataSource,
                makeItem = () =>
                {
                    var newListEntry = EditorResources.WeightedListElementAsset.Instantiate();

                    var newListEntryLogic = new WeightedListEntryController();
                    newListEntryLogic.SetVisualElement<TObject>(newListEntry);
                    newListEntry.userData = newListEntryLogic;
                
                    return newListEntry;
                }
            };
            _listView.AddToClassList("list-view");

            SetBindings(serializedRule, boundPropertyName);
            RegisterDragAndDrop();
        }

        private void SetBindings(SerializedProperty serializedRule, string boundPropertyName)
        {
            var boundProperty = serializedRule.FindPropertyRelative(boundPropertyName);
            
            _listView.bindItem = (item, index) =>
            {
                (item.userData as WeightedListEntryController)?.BindData<TObject>(index, boundProperty);
            };
            _listView.BindProperty(boundProperty);
        }

        private void RegisterDragAndDrop()
        {
            _listView.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (TryGetDraggedAssets(out _))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    evt.StopPropagation();
                }
            });

            _listView.RegisterCallback<DragPerformEvent>(evt =>
            {
                if (!TryGetDraggedAssets(out var assets)) return;

                DragAndDrop.AcceptDrag();
                
                var entries = assets.Where(o => IsAsset(o) && o is TObject)
                    .Select(o => _convertFunc.Invoke(o as TObject))
                    .ToList();
                
                AddEntries(entries);
                evt.StopPropagation();
            });
        }
        
        private void AddEntries(List<TEntry> entries)
        {
            if (entries.Count == 0) return;

            _boundList.AddRange(entries);
            _listView.Rebuild();

            HighlightLastElement();
        }

        private static bool IsAsset(Object o)
        {
            return o != null && EditorUtility.IsPersistent(o);
        }

        private static bool TryGetDraggedAssets(out List<Object> assets)
        {
            assets = DragAndDrop.objectReferences
                .Where(IsAsset)
                .Distinct()
                .ToList();

            return assets.Count > 0;
        }
        
        private void OnAddClicked()
        {
            _boundList.Add(new TEntry());
            _listView.Rebuild();
            
            HighlightLastElement();
        }

        private void OnRemoveClicked()
        {
            var indices = _listView.selectedIndices
                .OrderByDescending(x => x)
                .ToList();

            if (indices.Count == 0) return;

            foreach (var i in indices)
            {
                if (i >= 0 && i < _boundList.Count)
                {
                    _boundList.RemoveAt(i);
                }
            }

            _listView.Rebuild();
        }
        
        private void HighlightLastElement()
        {
            var last = _boundList.Count - 1;

            if (last >= 0)
            {
                _listView.SetSelection(new[] { last });
                _listView.ScrollToItem(last);
            }
        }
    }
}