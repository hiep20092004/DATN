using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.IO;

namespace WaterFlow.Core
{
    public class CustomList
    {
        //data
        private SerializedObject serializedObject;
        private SerializedProperty elementsProperty;
        private List<SerializedProperty> elementsList;
        private bool usingListInterface = false;
        private IList elements;

        private int minWidth = 150;

        /// <summary>
        /// When true, items flow into multiple columns when the list is wider than <see cref="multiColumnMinElementWidth"/>.
        /// </summary>
        public bool enableMultiColumnLayout;

        /// <summary>Minimum width per column before adding another column.</summary>
        public float multiColumnMinElementWidth = 160f;

        private int columnCount = 1;
        private float columnWidth;
        /// <summary>Visible rows per column in multi-column (column-major) layout.</summary>
        private int gridRowsPerColumn = 1;

        //global

        private int selectedIndex = -1;
        private int prevIndent;

        private Event currentEvent;
        private bool executedOnce;

        private bool usingPropertyList;
        private bool stretchHeight = true;
        private bool stretchWidth = true;
        public CustomListStyle style;
        private GUIStyle controlStyle;
        private GUIStyle elementHeaderLabelStyle;

        private Rect globalRect;
        private Rect footerPaginationRect;
        private Rect footerButtonsRect;
        private Rect listRect;
        private Rect filledElementsRect;


        //header
        private Rect headerRect;
        private Rect headerContentRect;

        //footer
        private float rightEdge;
        private float leftEdge;
        private Rect buttonsRect;
        private Rect footerButtonRect;

        //list & drag
        private bool dragging;
        private bool isSelected;
        private int fullIndex;
        private int startDragIndex;
        private int currentDragIndex;
        private int dragIndexAdjustment;
        private int maxElementCount;
        private float tempX;
        private float tempY;
        float draggedElementY;
        private float previousDraggedElementY;

        private float dragOffset;
        private bool isDraggedElementExpanded;
        private float draggedElemenentHeight;
        private float mouseYinList;
        private Rect listContentRect;
        private Rect elementRect;
        private Rect draggingHandleRect;
        private Rect elementHeaderRect;

        private Vector2 lastMouseDownPosition;
        private int lastMouseDownIndex;
        private float lastMouseDownHeight;
        private int pendingHeaderClickIndex = -1;
        private int pendingHeaderClickButton = -1;
        private static readonly float ClickMoveThreshold = 1f + Mathf.Epsilon;
        private float focusedElementHeight;
        private SerializedProperty focusedElementProperty;
        private int currenElementCount;
        private int tempElementCount;
        private int availableSlots;
        private int cachedArraySize;
        private float elementHeaderRectOffsetY;
        private float draggingHandleRectOffsetY;
        private float foldoutRectOffsetY;
        private float fieldRectOffsetY;
        private float[] fieldHeights;
        private float minPosiibleHeight;
        private float desiredHeight;
        private float restOfthelistHeight;

        //pagination
        private bool enablePagination;
        /// <summary>
        /// When true, the visible page is driven by pagination buttons instead of snapping to the selected item.
        /// </summary>
        private bool manualPageNavigation;
        private int currentPage;
        private int pagesCount;
        private int pageBeginIndex;
        private int currentBeginIndex;
        private int focusedElementIndex;
        private int pageElementCount;
        private Rect paginationContentRect;
        private Rect firstPageButtonRect;
        private Rect previousPageButtonRect;
        private Rect lastPageButtonRect;
        private Rect paginationLabelRect;
        private Rect nextPageButtonRect;


        #region delegates

        public delegate string GetHeaderLabelCallbackDelegate();

        public delegate void SelectionChangedCallbackDelegate();

        public delegate void ListChangedCallbackDelegate();

        public delegate void ListReorderedCallbackDelegate();

        public delegate void ListReorderedCallbackWithDetailsDelegate(int srcIndex, int dstIndex);

        public delegate void AddElementCallbackDelegate();

        public delegate void AddElementWithDropdownCallback();

        public delegate void RemoveElementCallbackDelegate();

        public delegate void DisplayContextMenuCallbackDelegate();

        public delegate void ListUndoCallbackDelegate();

        public delegate void DrawElementHeaderExtraDelegate(SerializedProperty elementProperty, int index, Rect rect);

        public GetHeaderLabelCallbackDelegate getHeaderLabelCallback;
        public SelectionChangedCallbackDelegate selectionChangedCallback;
        public ListReorderedCallbackDelegate listReorderedCallback;
        public ListReorderedCallbackWithDetailsDelegate listReorderedCallbackWithDetails;
        public ListChangedCallbackDelegate listChangedCallback;
        public AddElementCallbackDelegate addElementCallback;
        public AddElementWithDropdownCallback addElementWithDropdownCallback;
        public RemoveElementCallbackDelegate removeElementCallback;
        public DisplayContextMenuCallbackDelegate displayContextMenuCallback;
        public ListUndoCallbackDelegate listUndoCallback;
        public DrawElementHeaderExtraDelegate drawElementElementHeaderExtraCallback;
        public float headerExtraWidth = 18f;

        #endregion


        //element
        bool useFoldout;
        bool useLabelProperty;

        public delegate string GetLabelDelegate(SerializedProperty elementProperty, int elementIndex);

        private GetLabelDelegate getLabelCallback;
        private List<AbstractField> fields;
        private string labelPropertyName;
        private SerializedProperty currentElementProperty;
        private Rect bodyRect;
        private Rect labelRect;
        private Rect removeButtonRect;
        private Rect headerButtonRect;
        private Rect headerExtraRect;
        private Rect backgroundRect;
        private Rect calculatedGlobalRect;
        private Rect foldoutRect;
        private Rect fieldRect;
        private float bodyHeight;
        private bool ignoreDragEvents;
        private bool ignoreKeyboardArrows;
        private bool reorderConfirmationEnabled;

        private float CollapsedElementHeight => style.element.collapsedElementHeight;

        public int SelectedIndex
        {
            get => selectedIndex;
            set => selectedIndex = value;
        }

        /// <summary>
        /// When true, draws a text field + "Go To" row above the list (call <see cref="DrawJumpToIndexRow"/> before <see cref="Display"/>).
        /// </summary>
        public bool enableJumpToIndexRow;

        public string jumpToIndexFieldLabel = "Item #";
        public string jumpToIndexButtonLabel = "Go To";
        public float jumpToIndexButtonWidth = 64f;
        public float jumpToIndexLabelWidth = 0f;

        private string jumpToIndexInput = "";

        public int MinWidth
        {
            get => minWidth;
            set => minWidth = value;
        }

        public bool StretchHeight
        {
            get => stretchHeight;
            set => stretchHeight = value;
        }

        public bool StretchWidth
        {
            get => stretchWidth;
            set => stretchWidth = value;
        }

        public bool IgnoreDragEvents
        {
            get => ignoreDragEvents;
            set => ignoreDragEvents = value;
        }

        public bool IgnoreKeyboardArrows
        {
            get => ignoreKeyboardArrows;
            set => ignoreKeyboardArrows = value;
        }

        public bool ReorderConfirmationEnabled
        {
            get => reorderConfirmationEnabled;
            set => reorderConfirmationEnabled = value;
        }

        public CustomList(SerializedObject serializedObject, SerializedProperty elements, string labelPropertyName)
        {
            this.serializedObject = serializedObject;
            this.labelPropertyName = labelPropertyName;
            useLabelProperty = true;
            useFoldout = false;
            elementsProperty = elements;
            usingPropertyList = false;
            LoadStyle();
        }

        public CustomList(SerializedObject serializedObject, SerializedProperty elements,
            GetLabelDelegate getLabelCallback)
        {
            this.serializedObject = serializedObject;
            this.getLabelCallback = getLabelCallback;
            useLabelProperty = false;
            useFoldout = false;
            elementsProperty = elements;
            usingPropertyList = false;
            LoadStyle();
        }

        public CustomList(SerializedObject serializedObject, List<SerializedProperty> propertyList,
            string labelPropertyName)
        {
            this.serializedObject = serializedObject;
            this.labelPropertyName = labelPropertyName;
            useLabelProperty = true;
            useFoldout = false;
            elementsList = propertyList;
            usingPropertyList = true;
            LoadStyle();
        }

        public CustomList(SerializedObject serializedObject, List<SerializedProperty> propertyList,
            GetLabelDelegate getLabelCallback)
        {
            this.serializedObject = serializedObject;
            this.getLabelCallback = getLabelCallback;
            useLabelProperty = false;
            useFoldout = false;
            elementsList = propertyList;
            usingPropertyList = true;
            LoadStyle();
        }

        public CustomList(IList elements, GetLabelDelegate getLabelCallback)
        {
            this.getLabelCallback = getLabelCallback;
            useLabelProperty = false;
            useFoldout = false;
            this.elements = elements;
            usingListInterface = true;
            LoadStyle();
        }


        public void LoadStyle(int index = 0)
        {
            ListStylesDatabase listStylesDatabase = EditorUtils.GetAsset<ListStylesDatabase>();

            if (listStylesDatabase == null)
            {
                style = new CustomListStyle();
                style.SetDefaultStyleValues();
            }
            else
            {
                style = listStylesDatabase.GetStyle(index);
            }
        }

        public void AddPropertyField(string propertyName)
        {
            if (fields == null)
            {
                fields = new List<AbstractField>();
                useFoldout = true;
            }

            fields.Add(new PropertyField(propertyName));
        }

        public void AddPropertyField(string propertyName, GUIContent customGUIContent)
        {
            if (fields == null)
            {
                fields = new List<AbstractField>();
                useFoldout = true;
            }

            fields.Add(new PropertyField(propertyName, customGUIContent));
        }

        public void AddCustomField(CustomField.DrawCallbackDelegate drawCallbackDelegate,
            CustomField.GetHeightCallbackDelegate getHeightCallbackDelegate)
        {
            if (fields == null)
            {
                fields = new List<AbstractField>();
                useFoldout = true;
            }

            fields.Add(new CustomField(drawCallbackDelegate, getHeightCallbackDelegate));
        }

        public void AddSpace()
        {
            if (fields == null)
            {
                fields = new List<AbstractField>();
                useFoldout = true;
            }

            fields.Add(new Space());
        }

        public void AddSpace(float height)
        {
            if (fields == null)
            {
                fields = new List<AbstractField>();
                useFoldout = true;
            }

            fields.Add(new Space(height));
        }

        public void AddSeparator()
        {
            if (fields == null)
            {
                fields = new List<AbstractField>();
                useFoldout = true;
            }

            fields.Add(new Separator());
        }

        public void AddSeparator(Color color)
        {
            if (fields == null)
            {
                fields = new List<AbstractField>();
                useFoldout = true;
            }

            fields.Add(new Separator(color));
        }
        
        public void EnableHeader(GetHeaderLabelCallbackDelegate callback)
        {
            style.enableHeader = true;
            getHeaderLabelCallback = callback;
        }

        private int GetEffectiveColumnCount()
        {
            if (!enableMultiColumnLayout || useFoldout || columnCount <= 1)
            {
                return 1;
            }

            return columnCount;
        }

        private int GetRowsPerViewport(float listHeight, bool reservePaginationSpace)
        {
            float height = listHeight;

            if (reservePaginationSpace)
            {
                height -= style.pagination.height;
            }

            return Mathf.Max(1, Mathf.FloorToInt(height / CollapsedElementHeight));
        }

        private int GetGridRowsPerColumn()
        {
            int columns = GetEffectiveColumnCount();
            if (columns <= 1)
            {
                return 1;
            }

            return Mathf.Max(1, gridRowsPerColumn);
        }

        private static void GetColumnMajorGridPosition(int localIndex, int rowsPerColumn, out int column, out int row)
        {
            column = localIndex / rowsPerColumn;
            row = localIndex % rowsPerColumn;
        }

        private static int GetColumnMajorLocalIndex(int column, int row, int rowsPerColumn)
        {
            return column * rowsPerColumn + row;
        }

        /// <summary>
        /// Index at <paramref name="targetColumn"/> and <paramref name="row"/> on a page, or the last item in that column if the row is missing.
        /// </summary>
        private static int GetColumnRowIndexOnPage(
            int pageBegin,
            int itemsOnPage,
            int rowsPerColumn,
            int targetColumn,
            int row)
        {
            if (itemsOnPage <= 0)
            {
                return pageBegin;
            }

            int maxColumn = (itemsOnPage - 1) / rowsPerColumn;
            targetColumn = Mathf.Clamp(targetColumn, 0, maxColumn);
            int columnStartLocal = targetColumn * rowsPerColumn;
            int targetLocal = columnStartLocal + row;

            if (targetLocal < itemsOnPage)
            {
                return pageBegin + targetLocal;
            }

            int lastInColumn = Mathf.Min(columnStartLocal + rowsPerColumn, itemsOnPage) - 1;
            return pageBegin + lastInColumn;
        }

        private static int GetColumnMajorVisibleRowCount(int itemCount, int rowsPerColumn)
        {
            if (itemCount <= 0)
            {
                return 0;
            }

            if (itemCount <= rowsPerColumn)
            {
                return itemCount;
            }

            return rowsPerColumn;
        }

        private static int GetMultiColumnKeyboardNavigationIndex(
            int index,
            KeyCode keyCode,
            int rowsPerColumn,
            int columns,
            int arraySize,
            int pageBeginIndex,
            int pageElementCount)
        {
            int pageEndIndex = Mathf.Min(pageBeginIndex + pageElementCount, arraySize) - 1;
            int localIndex = index - pageBeginIndex;
            GetColumnMajorGridPosition(localIndex, rowsPerColumn, out int column, out int row);
            int itemsOnPage = pageEndIndex - pageBeginIndex + 1;
            int maxColumnOnPage = (itemsOnPage - 1) / rowsPerColumn;

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                case KeyCode.DownArrow:
                    return keyCode == KeyCode.UpArrow ? index - 1 : index + 1;
                case KeyCode.LeftArrow:
                    if (column == 0)
                    {
                        if (pageBeginIndex == 0)
                        {
                            return index - 1;
                        }

                        int prevPageBegin = pageBeginIndex - pageElementCount;
                        int itemsOnPrevPage = pageElementCount;
                        int lastColumnOnPrevPage = (itemsOnPrevPage - 1) / rowsPerColumn;
                        return GetColumnRowIndexOnPage(
                            prevPageBegin, itemsOnPrevPage, rowsPerColumn, lastColumnOnPrevPage, row);
                    }

                    return index - rowsPerColumn;
                case KeyCode.RightArrow:
                    if (column >= maxColumnOnPage)
                    {
                        int nextPageBegin = pageBeginIndex + pageElementCount;
                        if (nextPageBegin >= arraySize)
                        {
                            return index + 1;
                        }

                        int nextPageEnd = Mathf.Min(nextPageBegin + pageElementCount, arraySize) - 1;
                        int itemsOnNextPage = nextPageEnd - nextPageBegin + 1;
                        return GetColumnRowIndexOnPage(nextPageBegin, itemsOnNextPage, rowsPerColumn, 0, row);
                    }

                    if (index + rowsPerColumn > pageEndIndex)
                    {
                        return index + 1;
                    }

                    return index + rowsPerColumn;
                default:
                    return index;
            }
        }

        #region Data

        public bool IsExpanded(int index)
        {
            if (usingListInterface)
            {
                return false;
            }
            else
            {
                return GetElement(index).isExpanded;
            }
        }

        public void SetIsExpanded(int index, bool value)
        {
            if (usingListInterface)
            {
                return;
            }

            GetElement(index).isExpanded = value;
        }

        public SerializedProperty GetElement(int index)
        {
            if (usingListInterface)
            {
                return null;
            }

            if (index >= ArraySize() || (index < 0))
            {
                Debug.LogError("Retrieving index out of bounds:" + index);
            }

            if (usingPropertyList)
            {
                return elementsList[index];
            }
            else
            {
                return elementsProperty.GetArrayElementAtIndex(index);
            }
        }

        public int ArraySize()
        {
            if (usingListInterface)
            {
                return elements.Count;
            }
            else if (usingPropertyList)
            {
                return elementsList.Count;
            }
            else
            {
                return elementsProperty.arraySize;
            }
        }

        public void MoveElement(int srcIndex, int destIndex)
        {
            if (listReorderedCallbackWithDetails != null)
            {
                listReorderedCallbackWithDetails.Invoke(srcIndex, destIndex);
            }
            else
            {
                if (usingListInterface)
                {
                    UndoCallback();
                    var item = elements[srcIndex];
                    elements.RemoveAt(srcIndex);
                    elements.Insert(destIndex, item);
                }
                else if (usingPropertyList)
                {
                    UndoCallback();
                    SerializedProperty temp = elementsList[srcIndex];
                    elementsList.RemoveAt(srcIndex);
                    elementsList.Insert(destIndex, temp);
                }
                else
                {
                    elementsProperty.MoveArrayElement(srcIndex, destIndex);
                    serializedObject.ApplyModifiedProperties();
                }
            }

            listReorderedCallback?.Invoke();
            ListChangedCallback();
        }

        #endregion

        /// <summary>
        /// Selects an item and invokes <see cref="selectionChangedCallback"/> (same as clicking a row).
        /// </summary>
        public void SetSelectedIndexAndNotify(int zeroBasedIndex)
        {
            int count = ArraySize();
            if (count <= 0)
            {
                return;
            }

            OnSelectionChanged(Mathf.Clamp(zeroBasedIndex, 0, count - 1));
        }

        public void DrawJumpToIndexRow()
        {
            if (!enableJumpToIndexRow)
            {
                return;
            }

            EditorGUILayout.BeginHorizontal();
            float savedLabelWidth = EditorGUIUtility.labelWidth;
            if (jumpToIndexLabelWidth > 0f)
                EditorGUIUtility.labelWidth = jumpToIndexLabelWidth;
            jumpToIndexInput = EditorGUILayout.TextField(jumpToIndexFieldLabel, jumpToIndexInput);
            EditorGUIUtility.labelWidth = savedLabelWidth;
            if (GUILayout.Button(jumpToIndexButtonLabel, GUILayout.Width(jumpToIndexButtonWidth)))
            {
                TryApplyJumpToIndexInput();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void TryApplyJumpToIndexInput()
        {
            int count = ArraySize();
            if (count == 0)
            {
                return;
            }

            if (!int.TryParse(jumpToIndexInput.Trim(), out int parsed))
            {
                Debug.LogError("CustomList: could not parse index.");
                return;
            }

            if (parsed < 1)
            {
                Debug.LogError("CustomList: enter a valid index from 1.");
                return;
            }

            if (parsed > count)
            {
                Debug.LogError($"CustomList: index {parsed} is out of range (count: {count}).");
                return;
            }

            int zeroBased = parsed - 1;
            OnSelectionChanged(zeroBased);
        }


        public void Display()
        {
            ExecuteStuffOnce();

            currentEvent = Event.current;
            cachedArraySize = ArraySize();
            prevIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            HandleKeyboardArrows();

            DoCalculations();
            HandleDrawingBackgroundConfiguration(globalRect, style.globalBackground);


            if (style.enableHeader)
            {
                DrawHeader();
            }

            DrawList();

            if (enablePagination)
            {
                DrawPagination();
            }

            if (style.enableFooterAddButton || style.enableFooterRemoveButton)
            {
                DrawFooterButtons();
            }


            EditorGUI.indentLevel = prevIndent;

            if (currentEvent.isMouse && (cachedArraySize > 0) && (currentEvent.type != EventType.Used) &&
                (!ignoreDragEvents))
            {
                HandleDraggingDetection();
            }

            TryCompleteHeaderClick();
        }

        private void HandleKeyboardArrows()
        {
            if ((Event.current.type != EventType.KeyDown) || IgnoreKeyboardArrows || (cachedArraySize == 0))
            {
                return;
            }

            KeyCode keyCode = Event.current.keyCode;
            if (keyCode != KeyCode.UpArrow && keyCode != KeyCode.DownArrow && keyCode != KeyCode.LeftArrow &&
                keyCode != KeyCode.RightArrow)
            {
                return;
            }

            int arraySize = cachedArraySize;
            int index = selectedIndex < 0 ? 0 : selectedIndex;
            int newIndex;

            int columns = GetEffectiveColumnCount();
            if (columns > 1)
            {
                int pageBegin = enablePagination ? currentPage * pageElementCount : 0;
                newIndex = GetMultiColumnKeyboardNavigationIndex(
                    index, keyCode, GetGridRowsPerColumn(), columns, arraySize, pageBegin, pageElementCount);
            }
            else if (keyCode == KeyCode.DownArrow)
            {
                newIndex = index + 1;
            }
            else if (keyCode == KeyCode.UpArrow)
            {
                newIndex = index - 1;
            }
            else if (keyCode == KeyCode.RightArrow)
            {
                newIndex = index + pageElementCount;
            }
            else
            {
                newIndex = index - pageElementCount;
            }

            newIndex = Mathf.Clamp(newIndex, 0, arraySize - 1);

            if (newIndex != index)
            {
                OnSelectionChanged(newIndex);
            }

            Event.current.Use();
        }

        private void ExecuteStuffOnce()
        {
            if (!executedOnce) //stuff that called once
            {
                executedOnce = true;
                CollapseElements();

                if (useFoldout)
                {
                    fieldHeights = new float[fields.Count];
                }


                //style for control
                controlStyle = new GUIStyle();
                controlStyle.stretchHeight = stretchHeight;
                controlStyle.stretchWidth = stretchWidth;

                elementHeaderLabelStyle = new GUIStyle(EditorStyles.label);
                elementHeaderLabelStyle.richText = true;


                //calculating prefered height
                restOfthelistHeight = 0;

                if (style.enableHeader)
                {
                    restOfthelistHeight += style.header.height;
                }

                restOfthelistHeight += style.footerButtons.height;
                restOfthelistHeight += style.list.contentPaddingBottom;
                restOfthelistHeight += style.list.contentPaddingTop;

                //init rects
                listRect = new Rect();
                headerRect = new Rect();
                footerButtonsRect = new Rect();
                listContentRect = new Rect();
                footerPaginationRect = new Rect();
                filledElementsRect = new Rect();
                backgroundRect = new Rect();
                calculatedGlobalRect = new Rect();
                elementHeaderRect = new Rect();
                globalRect = new Rect();
                footerPaginationRect = new Rect();
                footerButtonsRect = new Rect();
                headerContentRect = new Rect();
                buttonsRect = new Rect();
                footerButtonRect = new Rect();
                elementRect = new Rect();
                draggingHandleRect = new Rect();
                paginationContentRect = new Rect();
                firstPageButtonRect = new Rect();
                previousPageButtonRect = new Rect();
                lastPageButtonRect = new Rect();
                paginationLabelRect = new Rect();
                nextPageButtonRect = new Rect();
                bodyRect = new Rect();
                labelRect = new Rect();
                removeButtonRect = new Rect();
                headerButtonRect = new Rect();
                headerExtraRect = new Rect();
                foldoutRect = new Rect();
                fieldRect = new Rect();
            }
        }


        private void DoCalculations()
        {
            int arraySize = cachedArraySize;

            //Attempt to minimize the list
            if (arraySize > 0)
            {
                focusedElementIndex = Mathf.Clamp(selectedIndex, 0, arraySize - 1);
                CalculateFocusedElementHeight();

                if (focusedElementHeight > CollapsedElementHeight)
                {
                    minPosiibleHeight = focusedElementHeight + restOfthelistHeight;
                    desiredHeight = (arraySize - 1) * CollapsedElementHeight + minPosiibleHeight;
                }
                else
                {
                    minPosiibleHeight = CollapsedElementHeight + style.pagination.height + restOfthelistHeight;
                    desiredHeight = arraySize * CollapsedElementHeight + restOfthelistHeight;
                }
            }
            else
            {
                focusedElementHeight = 20f; //For a label;
                minPosiibleHeight = focusedElementHeight + restOfthelistHeight;
                desiredHeight = minPosiibleHeight;
            }

            globalRect = GUILayoutUtility.GetRect(GUIContent.none, controlStyle, GUILayout.MinHeight(minPosiibleHeight),
                GUILayout.MinWidth(MinWidth), GUILayout.MaxHeight(desiredHeight));

            if (globalRect.height < 5) // I don`t remember if it`s zero or one in some cases so I put 5
            {
                return;
            }

            if (!((currentEvent.type == EventType.Layout) || (currentEvent.type == EventType.Repaint)))
            {
                return;
            }

            if (calculatedGlobalRect != globalRect) // we skip some calculations
            {
                listRect.Set(globalRect.x, globalRect.y, globalRect.width, globalRect.height);
                calculatedGlobalRect.Set(globalRect.x, globalRect.y, globalRect.width, globalRect.height);


                if (style.enableHeader)
                {
                    headerRect.Set(globalRect.x, globalRect.y, globalRect.width, style.header.height);

                    headerContentRect.x = style.header.contentPaddingLeft + headerRect.x;
                    headerContentRect.y = style.header.contentPaddingTop + headerRect.y;
                    headerContentRect.xMax = headerRect.xMax - style.header.contentPaddingRight;
                    headerContentRect.yMax = headerRect.yMax - style.header.contentPaddingBottom;
                    listRect.yMin += style.header.height;
                }

                //add footer
                footerButtonsRect.Set(globalRect.x, globalRect.y, globalRect.width, globalRect.height);
                footerButtonsRect.yMin = footerButtonsRect.yMax - style.footerButtons.height;
                listRect.yMax -= style.footerButtons.height;


                //max number of elements with pagination (if all of them isn`t expadned)
                listContentRect.x = style.list.contentPaddingLeft + listRect.x;
                listContentRect.y = style.list.contentPaddingTop + listRect.y;
                listContentRect.xMax = listRect.xMax - style.list.contentPaddingRight;
                listContentRect.yMax = listRect.yMax - style.list.contentPaddingBottom;

                columnCount = 1;
                columnWidth = listContentRect.width;

                if (enableMultiColumnLayout && !useFoldout)
                {
                    columnCount = Mathf.Max(1,
                        Mathf.FloorToInt(listContentRect.width / multiColumnMinElementWidth));
                    columnWidth = listContentRect.width / columnCount;
                }

                gridRowsPerColumn = GetRowsPerViewport(listContentRect.height, reservePaginationSpace: true);
                int rowsMax = GetRowsPerViewport(listContentRect.height, reservePaginationSpace: false);
                pageElementCount = gridRowsPerColumn * GetEffectiveColumnCount();
                maxElementCount = rowsMax * GetEffectiveColumnCount();
                enablePagination = (arraySize > maxElementCount);


                //add space for pagination
                if (enablePagination)
                {
                    footerPaginationRect.Set(globalRect.position.x,
                        footerButtonsRect.position.y - style.pagination.height, globalRect.width,
                        style.pagination.height);
                    listRect.yMax -= style.pagination.height;
                    listContentRect.yMax -= style.pagination.height;

                    //Calculate other rects for pagination
                    paginationContentRect.x = style.pagination.contentPaddingLeft + footerPaginationRect.x;
                    paginationContentRect.y = style.pagination.contentPaddingTop + footerPaginationRect.y;
                    paginationContentRect.xMax = footerPaginationRect.xMax - style.pagination.contentPaddingRight;
                    paginationContentRect.yMax = footerPaginationRect.yMax - style.pagination.contentPaddingBottom;


                    firstPageButtonRect.Set(paginationContentRect.xMin, paginationContentRect.y,
                        style.pagination.buttonsWidth, style.pagination.buttonsHeight);
                    previousPageButtonRect.Set(firstPageButtonRect.xMax, paginationContentRect.y,
                        style.pagination.buttonsWidth, style.pagination.buttonsHeight);

                    nextPageButtonRect.Set(paginationContentRect.xMax - (2 * style.pagination.buttonsWidth),
                        paginationContentRect.y, style.pagination.buttonsWidth, style.pagination.buttonsHeight);
                    lastPageButtonRect.Set(paginationContentRect.xMax - style.pagination.buttonsWidth,
                        paginationContentRect.y, style.pagination.buttonsWidth, style.pagination.buttonsHeight);
                    paginationLabelRect.Set(previousPageButtonRect.xMax, paginationContentRect.y,
                        nextPageButtonRect.xMin - previousPageButtonRect.xMax, style.pagination.labelHeight);
                }


                //calculate some rects for optimization
                elementHeaderRect.x = style.element.headerPaddingLeft + listContentRect.x;
                elementHeaderRect.y = style.element.headerPaddingTop + listContentRect.y;
                elementHeaderRect.xMax = listContentRect.xMax - style.element.headerPaddingRight;
                elementHeaderRect.height = CollapsedElementHeight - style.element.headerPaddingBottom;

                draggingHandleRect.Set(elementHeaderRect.x + style.dragHandle.paddingLeft, elementHeaderRect.y,
                    style.dragHandle.width, style.dragHandle.height);
                draggingHandleRect.y = elementHeaderRect.y + elementHeaderRect.height - style.dragHandle.paddingBottom -
                                       style.dragHandle.height;

                labelRect.Set(elementHeaderRect.x, elementHeaderRect.y, elementHeaderRect.width,
                    elementHeaderRect.height);
                labelRect.xMin += style.dragHandle.allocatedHorizontalSpace;

                headerButtonRect.Set(elementHeaderRect.x, elementHeaderRect.y, elementHeaderRect.width,
                    elementHeaderRect.height);

                if (useFoldout)
                {
                    foldoutRect.Set(
                        elementHeaderRect.x + style.foldout.paddingLeft + style.dragHandle.allocatedHorizontalSpace,
                        elementHeaderRect.y, style.foldout.width, style.foldout.height);
                    foldoutRect.y = elementHeaderRect.y + elementHeaderRect.height - style.foldout.paddingBottom -
                                    style.foldout.height;
                    labelRect.xMin += style.foldout.allocatedHorizontalSpace;
                }

                if (drawElementElementHeaderExtraCallback != null)
                {
                    float extraSpace = headerExtraWidth + 2f;
                    labelRect.xMax -= extraSpace;
                    headerButtonRect.xMax -= extraSpace;
                    headerExtraRect.Set(labelRect.xMax, elementHeaderRect.y, headerExtraWidth, elementHeaderRect.height);
                }

                if (style.enableElementRemoveButton)
                {
                    removeButtonRect.Set(
                        elementHeaderRect.xMax + style.removeElementButton.paddingLeft -
                        style.removeElementButton.allocatedHorizontalSpace, elementHeaderRect.y,
                        style.removeElementButton.width, style.removeElementButton.height);
                    labelRect.xMax -= style.removeElementButton.allocatedHorizontalSpace;
                    headerButtonRect.xMax -= style.removeElementButton.allocatedHorizontalSpace;
                }

                bodyRect.x = style.element.bodyPaddingLeft + listContentRect.x;
                bodyRect.y = style.element.bodyPaddingTop + style.element.collapsedElementHeight + listContentRect.y;
                bodyRect.xMax = listContentRect.xMax - style.element.bodyPaddingRight;

                fieldRect.Set(bodyRect.x, bodyRect.y, bodyRect.width, 0);

                elementHeaderRectOffsetY = elementHeaderRect.y - listContentRect.y;
                draggingHandleRectOffsetY = draggingHandleRect.y - listContentRect.y;
                foldoutRectOffsetY = foldoutRect.y - listContentRect.y;
                fieldRectOffsetY = bodyRect.y - listContentRect.y;
            }

            if (enablePagination)
            {
                //dealing with pages
                pagesCount = Mathf.CeilToInt((arraySize + 0f) / pageElementCount);

                if (pagesCount > 1) // fix to layout event bug
                {
                    currentPage = Mathf.Clamp(currentPage, 0, pagesCount - 1);

                    if (selectedIndex != -1 && !manualPageNavigation)
                    {
                        // Keep selected element in view when the list is resized or selection changes.
                        currentPage = Mathf.FloorToInt((selectedIndex + 0f) / pageElementCount);
                    }
                }

                pageBeginIndex = currentPage * pageElementCount;
                currentBeginIndex = pageBeginIndex;
            }
            else
            {
                currentPage = 0;
                pageBeginIndex = 0;
                currentBeginIndex = 0;
                manualPageNavigation = false;
            }

        if (arraySize > 0)
        {
            if (GetEffectiveColumnCount() > 1)
            {
                // Multi-column: all items have the same height (foldout is disabled).
                // Show exactly the items belonging to the current page without the
                // focused-element scroll offset logic, which would otherwise under-count
                // by (columns - 1) items due to double-accounting of pagination height.
                focusedElementIndex = Mathf.Clamp(selectedIndex, pageBeginIndex, arraySize - 1);
                focusedElementHeight = CollapsedElementHeight;
                currentBeginIndex = pageBeginIndex;
                currenElementCount = Mathf.Min(pageElementCount, arraySize - pageBeginIndex);
            }
            else
            {
                //get height of focused element
                if (manualPageNavigation && enablePagination)
                {
                    int pageEndIndex = Mathf.Min(pageBeginIndex + pageElementCount, arraySize) - 1;
                    if (selectedIndex >= pageBeginIndex && selectedIndex <= pageEndIndex)
                        focusedElementIndex = selectedIndex;
                    else
                        focusedElementIndex = pageBeginIndex;
                }
                else
                {
                    focusedElementIndex = Mathf.Clamp(selectedIndex, pageBeginIndex, arraySize - 1);
                }

                CalculateFocusedElementHeight();

                int rowsInViewport = Mathf.FloorToInt((listContentRect.height - focusedElementHeight) /
                                                    CollapsedElementHeight);
                availableSlots = rowsInViewport;

                if (listContentRect.height < focusedElementHeight)
                {
                    Debug.LogError(
                        "Element is to large to fit into the list. Maybe use MinHeight property to increase height of the list or show less fields of element");
                }

                currenElementCount = availableSlots + 1; //1 for our focused element

                //deal with elements before focused element

                if (focusedElementIndex > pageBeginIndex)
                {
                    tempElementCount = focusedElementIndex - pageBeginIndex;

                    if (availableSlots >= tempElementCount)
                    {
                        availableSlots -= tempElementCount;
                        currentBeginIndex = pageBeginIndex;
                    }
                    else
                    {
                        currentBeginIndex = focusedElementIndex - availableSlots;
                        availableSlots = 0;
                    }
                }

                //deal with elements after focused element
                tempElementCount = arraySize - 1 - focusedElementIndex;

                if (availableSlots > tempElementCount)
                {
                    currenElementCount -= availableSlots - tempElementCount;
                }
            }
        }
        else
        {
            currenElementCount = 1;
        }


            int effectiveColumns = GetEffectiveColumnCount();
            int visibleRows = effectiveColumns > 1
                ? GetColumnMajorVisibleRowCount(currenElementCount, GetGridRowsPerColumn())
                : currenElementCount;
            float filledHeight = effectiveColumns > 1
                ? (visibleRows - 1) * CollapsedElementHeight + focusedElementHeight
                : (currenElementCount - 1) * CollapsedElementHeight + focusedElementHeight;
            filledElementsRect.Set(listContentRect.x, listContentRect.y, listContentRect.width, filledHeight);
        }

        private void ApplyOffsetsFromElementRect(Rect elementLayoutRect)
        {
            elementHeaderRect.x = style.element.headerPaddingLeft + elementLayoutRect.x;
            elementHeaderRect.y = style.element.headerPaddingTop + elementLayoutRect.y;
            elementHeaderRect.width = elementLayoutRect.width - style.element.headerPaddingLeft -
                                      style.element.headerPaddingRight;
            elementHeaderRect.height = CollapsedElementHeight - style.element.headerPaddingBottom;

            draggingHandleRect.Set(elementHeaderRect.x + style.dragHandle.paddingLeft, elementHeaderRect.y,
                style.dragHandle.width, style.dragHandle.height);
            draggingHandleRect.y = elementHeaderRect.y + elementHeaderRect.height - style.dragHandle.paddingBottom -
                                   style.dragHandle.height;

            labelRect.Set(elementHeaderRect.x, elementHeaderRect.y, elementHeaderRect.width, elementHeaderRect.height);
            labelRect.xMin += style.dragHandle.allocatedHorizontalSpace;

            headerButtonRect.Set(elementHeaderRect.x, elementHeaderRect.y, elementHeaderRect.width,
                elementHeaderRect.height);

            if (useFoldout)
            {
                foldoutRect.Set(
                    elementHeaderRect.x + style.foldout.paddingLeft + style.dragHandle.allocatedHorizontalSpace,
                    elementHeaderRect.y, style.foldout.width, style.foldout.height);
                foldoutRect.y = elementHeaderRect.y + elementHeaderRect.height - style.foldout.paddingBottom -
                                style.foldout.height;
                labelRect.xMin += style.foldout.allocatedHorizontalSpace;
            }

            if (drawElementElementHeaderExtraCallback != null)
            {
                float extraSpace = headerExtraWidth + 2f;
                labelRect.xMax -= extraSpace;
                headerButtonRect.xMax -= extraSpace;
                headerExtraRect.Set(labelRect.xMax, elementHeaderRect.y, headerExtraWidth, elementHeaderRect.height);
            }

            if (style.enableElementRemoveButton)
            {
                removeButtonRect.Set(
                    elementHeaderRect.xMax + style.removeElementButton.paddingLeft -
                    style.removeElementButton.allocatedHorizontalSpace, elementHeaderRect.y,
                    style.removeElementButton.width, style.removeElementButton.height);
                labelRect.xMax -= style.removeElementButton.allocatedHorizontalSpace;
                headerButtonRect.xMax -= style.removeElementButton.allocatedHorizontalSpace;
            }

            bodyRect.x = style.element.bodyPaddingLeft + elementLayoutRect.x;
            bodyRect.y = style.element.bodyPaddingTop + elementLayoutRect.y + CollapsedElementHeight;
            bodyRect.xMax = elementLayoutRect.xMax - style.element.bodyPaddingRight;
            fieldRect.Set(bodyRect.x, bodyRect.y, bodyRect.width, 0);
        }

        private void DrawHeader()
        {
            HandleDrawingBackgroundConfiguration(headerRect, style.header.backgroundConfiguration);
            if (currentEvent.type == EventType.Repaint)
            {
                EditorGUI.LabelField(headerContentRect, GetHeaderLabel(), style.header.labelStyle);
            }
        }

        private Rect GetElementLayoutRect(int index, float height)
        {
            int localIndex = index - currentBeginIndex;
            int columns = GetEffectiveColumnCount();

            if (columns <= 1)
            {
                return new Rect(listContentRect.x, listContentRect.y, listContentRect.width, height);
            }

            int rowsPerColumn = GetGridRowsPerColumn();
            GetColumnMajorGridPosition(localIndex, rowsPerColumn, out int column, out int row);
            return new Rect(
                listContentRect.x + column * columnWidth,
                listContentRect.y + row * CollapsedElementHeight,
                columnWidth,
                height);
        }

        private void DrawList()
        {
            HandleDrawingBackgroundConfiguration(listRect, style.list.backgroundConfiguration);
            tempX = listContentRect.position.x;
            tempY = listContentRect.position.y;
            int columns = GetEffectiveColumnCount();
            bool useGridLayout = columns > 1;

            if (cachedArraySize == 0)
            {
                HandleEmptyArray();
                return;
            }

            if (dragging)
            {
                for (int i = currentBeginIndex; i < currentBeginIndex + currenElementCount; i++)
                {
                    fullIndex = i;
                    HandleDragIndexAdjustments(i);

                    if (i == currentDragIndex)
                    {
                        isSelected = true;
                        fullIndex = startDragIndex;
                        float dragWidth = useGridLayout ? columnWidth : filledElementsRect.width;
                        elementRect.Set(tempX, draggedElementY, dragWidth, draggedElemenentHeight);
                        DrawElement(elementRect, isSelected, fullIndex);
                    }
                    else
                    {
                        isSelected = false;

                        if (useGridLayout)
                        {
                            elementRect = GetElementLayoutRect(fullIndex, CollapsedElementHeight);
                        }
                        else
                        {
                            elementRect.Set(tempX, tempY, listContentRect.width, CollapsedElementHeight);
                            tempY += elementRect.height;
                        }

                        DrawElement(elementRect, isSelected, fullIndex);
                    }
                }
            }
            else
            {
                for (int i = currentBeginIndex; i < currentBeginIndex + currenElementCount; i++)
                {
                    isSelected = (i == selectedIndex);
                    float elementHeight = isSelected && useFoldout ? focusedElementHeight : CollapsedElementHeight;

                    if (useGridLayout)
                    {
                        elementRect = GetElementLayoutRect(i, elementHeight);
                    }
                    else
                    {
                        elementRect.Set(tempX, tempY, listContentRect.width, elementHeight);
                        tempY += elementRect.height;
                    }

                    DrawElement(elementRect, isSelected, i);
                }
            }
        }

        private void HandleDragIndexAdjustments(int i)
        {
            if (i == currentDragIndex)
            {
                tempY += draggedElemenentHeight;
            }


            if (dragIndexAdjustment == 1)
            {
                if ((i >= currentDragIndex) && (i <= startDragIndex))
                {
                    fullIndex--;
                }
            }
            else if (dragIndexAdjustment == -1)
            {
                if ((i >= startDragIndex) && (i <= currentDragIndex))
                {
                    fullIndex++;
                }
            }
        }

        private void HandleEmptyArray()
        {
            elementRect.Set(listContentRect.x, listContentRect.y, listContentRect.width, CollapsedElementHeight);
            GUI.Label(elementRect, style.EMPTY_LIST_LABEL);
        }

        #region Element

        private void DrawElement(Rect rect, bool isSelected, int index)
        {
            currentElementProperty = GetElement(index);
            ApplyOffsetsFromElementRect(rect);
            DrawElementHeader(currentElementProperty, index, rect, style);

            //index < currentBeginIndex + currenElementCount is a fix that helps when user deletes element with header button
            if (isSelected && useFoldout && (IsExpanded(index)) && (index < currentBeginIndex + currenElementCount))
            {
                DrawElementBody(currentElementProperty, style);
            }

            if ((!dragging) && currentEvent.type == EventType.MouseDown && rect.Contains(currentEvent.mousePosition))
            {
                lastMouseDownPosition = currentEvent.mousePosition;
                lastMouseDownIndex = index;
                lastMouseDownHeight = rect.height;
            }
        }

        private void DrawElementHeader(SerializedProperty currentElementProperty, int index, Rect elementRect,
            CustomListStyle style)
        {
            if (Event.current.type == EventType.Repaint)
            {
                DrawElementBackground(elementHeaderRect, isSelected, style);
                style.dragHandle.guiStyle.Draw(draggingHandleRect, false, false, false, false);

                if (useFoldout)
                {
                    style.foldout.guiStyle.Draw(foldoutRect, false, currentElementProperty.isExpanded,
                        currentElementProperty.isExpanded, currentElementProperty.isExpanded);
                }
            }

            if (currentEvent.type == EventType.Repaint)
            {
                if (useLabelProperty)
                {
                    GUI.Label(labelRect, currentElementProperty.FindPropertyRelative(labelPropertyName).stringValue,
                        elementHeaderLabelStyle);
                }
                else if (getLabelCallback != null)
                {
                    GUI.Label(labelRect, getLabelCallback(currentElementProperty, index), elementHeaderLabelStyle);
                }
            }

            if (drawElementElementHeaderExtraCallback != null && currentElementProperty != null)
            {
                drawElementElementHeaderExtraCallback(currentElementProperty, index, headerExtraRect);
            }

            if (style.enableElementRemoveButton)
            {
                GUI.Label(removeButtonRect, style.removeElementButton.content, style.removeElementButton.guiStyle);

                if ((!dragging) && (currentEvent.type == EventType.MouseUp) &&
                    removeButtonRect.Contains(currentEvent.mousePosition))
                {
                    selectedIndex = index;
                    RemoveElement();
                    currentEvent.Use();
                }
            }


            headerButtonRect.Set(elementHeaderRect.x, elementHeaderRect.y, elementHeaderRect.width,
                elementHeaderRect.height);
            headerButtonRect.xMax = labelRect.xMax;

            if ((!dragging) && currentEvent.type == EventType.MouseDown &&
                headerButtonRect.Contains(currentEvent.mousePosition))
            {
                pendingHeaderClickIndex = index;
                pendingHeaderClickButton = currentEvent.button;
            }
        }

        /// <summary>
        /// Confirms header selection on MouseUp using the index hit on MouseDown (not MouseUp position).
        /// </summary>
        private void TryCompleteHeaderClick()
        {
            if (currentEvent.type != EventType.MouseUp || dragging || pendingHeaderClickIndex < 0)
            {
                return;
            }

            if (!globalRect.Contains(currentEvent.mousePosition))
            {
                pendingHeaderClickIndex = -1;
                return;
            }

            if ((lastMouseDownPosition - currentEvent.mousePosition).magnitude > ClickMoveThreshold)
            {
                pendingHeaderClickIndex = -1;
                return;
            }

            int index = pendingHeaderClickIndex;
            int button = pendingHeaderClickButton;
            pendingHeaderClickIndex = -1;
            pendingHeaderClickButton = -1;

            if (index < 0 || index >= cachedArraySize)
            {
                return;
            }

            if (selectedIndex != index)
            {
                OnSelectionChanged(index);
                EditorGUIUtility.keyboardControl = -1;
            }
            else
            {
                selectionChangedCallback?.Invoke();
            }

            if (button == 0)
            {
                if (useFoldout)
                {
                    SerializedProperty element = GetElement(index);
                    element.isExpanded = !element.isExpanded;
                }
            }
            else if (button == 1)
            {
                DisplayContextMenu();
            }

            currentEvent.Use();
        }

        private void DrawElementBody(SerializedProperty currentElementProperty, CustomListStyle style)
        {
            HandleDrawingBackgroundConfiguration(bodyRect, style.element.elementBodyBackgroundConfiguration);

            for (int i = 0; i < fields.Count; i++)
            {
                fieldRect.height = fieldHeights[i];
                fields[i].Draw(currentElementProperty, fieldRect, style);
                fieldRect.y += fieldRect.height;
            }
        }

        private void DrawElementBackground(Rect rect, bool isSelected, CustomListStyle style)
        {
            foreach (CustomListStyle.ElementColorRect colorRect in style.element.elementHeaderBackgroundConfiguration
                         .rects)
            {
                if ((colorRect.drawType == CustomListStyle.DrawType.DrawWhenSelected) && (!isSelected))
                {
                    continue;
                }

                if ((colorRect.drawType == CustomListStyle.DrawType.DrawWhenUnselected) && (isSelected))
                {
                    continue;
                }

                backgroundRect.x = colorRect.paddingLeft + rect.x;
                backgroundRect.y = colorRect.paddingTop + rect.y;
                backgroundRect.xMax = rect.xMax - colorRect.paddingRight;
                backgroundRect.yMax = rect.yMax - colorRect.paddingBottom;

                if (colorRect.rectType == CustomListStyle.RectType.ColorRect)
                {
                    EditorGUI.DrawRect(backgroundRect, colorRect.color);
                }
                else if (colorRect.rectType == CustomListStyle.RectType.Border)
                {
                    GUI.DrawTexture(backgroundRect, EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill, true, 0,
                        colorRect.color, colorRect.borderWidth, colorRect.borderRadius);
                }
                else if (colorRect.rectType == CustomListStyle.RectType.RoundRect)
                {
                    GUI.DrawTexture(backgroundRect, EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill, true, 0,
                        colorRect.color, colorRect.borderWidth * 100, colorRect.borderRadius);
                }
            }
        }

        private void CalculateFocusedElementHeight()
        {
            focusedElementProperty = GetElement(focusedElementIndex);

            if (useFoldout && IsExpanded(focusedElementIndex))
            {
                bodyHeight = 0;

                for (int i = 0; i < fields.Count; i++)
                {
                    fieldHeights[i] = fields[i].GetHeight(focusedElementProperty, style);
                    bodyHeight += fieldHeights[i];
                }

                bodyHeight += style.element.bodyPaddingTop + style.element.bodyPaddingBottom;
                focusedElementHeight = style.element.collapsedElementHeight + bodyHeight;
            }
            else
            {
                focusedElementHeight = CollapsedElementHeight;
            }
        }

        #endregion

        #region style functions

        private void HandleDrawingBackgroundConfiguration(Rect rect,
            CustomListStyle.BackgroundConfiguration configuration)
        {
            if (currentEvent.type == EventType.Repaint && (configuration.rects != null))
            {
                foreach (CustomListStyle.ColorRect colorRect in configuration.rects)
                {
                    backgroundRect.x = colorRect.paddingLeft + rect.x;
                    backgroundRect.y = colorRect.paddingTop + rect.y;
                    backgroundRect.xMax = rect.xMax - colorRect.paddingRight;
                    backgroundRect.yMax = rect.yMax - colorRect.paddingBottom;

                    if (colorRect.rectType == CustomListStyle.RectType.ColorRect)
                    {
                        EditorGUI.DrawRect(backgroundRect, colorRect.color);
                    }
                    else if (colorRect.rectType == CustomListStyle.RectType.Border)
                    {
                        GUI.DrawTexture(backgroundRect, EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill, true, 0,
                            colorRect.color, colorRect.borderWidth, colorRect.borderRadius);
                    }
                    else if (colorRect.rectType == CustomListStyle.RectType.RoundRect)
                    {
                        GUI.DrawTexture(backgroundRect, EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill, true, 0,
                            colorRect.color, colorRect.borderWidth * 100, colorRect.borderRadius);
                    }
                }
            }
        }

        #endregion


        #region Drag

        private void HandleDraggingDetection()
        {
            if (!dragging)
            {
                if ((currentEvent.type == EventType.MouseDrag) && globalRect.Contains(currentEvent.mousePosition) &&
                    (currentEvent.delta.magnitude < 5f) &&
                    ((lastMouseDownPosition - currentEvent.mousePosition).magnitude <= ClickMoveThreshold)) // 5f is very arbitrary number and this condition is here to fix bug with Object field
                {
                    DraggingStarted();
                }
            }
            else
            {
                if (currentEvent.type == EventType.MouseDrag)
                {
                    UpdateDrag();
                }
                else if (currentEvent.type == EventType.MouseUp)
                {
                    DraggingFinished();
                }
            }
        }

        private void DraggingStarted()
        {
            dragging = true;
            pendingHeaderClickIndex = -1;
            pendingHeaderClickButton = -1;
            startDragIndex = lastMouseDownIndex;
            currentDragIndex = startDragIndex;
            draggedElemenentHeight = lastMouseDownHeight;
            draggedElementY = Mathf.Clamp(currentEvent.mousePosition.y - dragOffset, filledElementsRect.yMin,
                filledElementsRect.yMax - draggedElemenentHeight);
            dragOffset = lastMouseDownPosition.y - draggedElementY;
            isDraggedElementExpanded = IsExpanded(startDragIndex);
            currentEvent.Use();
        }

        private void UpdateDrag()
        {
            int columns = GetEffectiveColumnCount();

            if (columns > 1)
            {
                float mouseXinList = currentEvent.mousePosition.x - listContentRect.position.x;
                mouseYinList = currentEvent.mousePosition.y - listContentRect.position.y;
                int rowsPerColumn = GetGridRowsPerColumn();
                int col = Mathf.Clamp(Mathf.FloorToInt(mouseXinList / columnWidth), 0, columns - 1);
                int row = Mathf.Clamp(Mathf.FloorToInt(mouseYinList / CollapsedElementHeight), 0, rowsPerColumn - 1);
                int localIndex = GetColumnMajorLocalIndex(col, row, rowsPerColumn);
                currentDragIndex = currentBeginIndex + localIndex;
            }
            else
            {
                mouseYinList = currentEvent.mousePosition.y - listContentRect.position.y;
                currentDragIndex = currentBeginIndex + Mathf.RoundToInt(mouseYinList / CollapsedElementHeight);
            }

            currentDragIndex = Mathf.Clamp(currentDragIndex, currentBeginIndex,
                currentBeginIndex + currenElementCount - 1);
            draggedElementY = Mathf.Clamp(currentEvent.mousePosition.y - dragOffset, filledElementsRect.yMin,
                filledElementsRect.yMax - draggedElemenentHeight);


            if (currentDragIndex == startDragIndex)
            {
                dragIndexAdjustment = 0;
            }
            else if (currentDragIndex < startDragIndex)
            {
                dragIndexAdjustment = 1;
            }
            else
            {
                dragIndexAdjustment = -1;
            }

            previousDraggedElementY = draggedElementY;
            currentEvent.Use();
        }

        private void RequestRepaint()
        {
            if (usingListInterface)
            {
                return;
            }

            EditorUtility.SetDirty(serializedObject.targetObject);
        }

        private void DraggingFinished()
        {
            dragging = false;

            if (ReorderConfirmationEnabled)
            {
                if (!EditorUtility.DisplayDialog("List reorder confirmation",
                        $"Are you sure you want to move the element from position #{startDragIndex + 1} to position #{currentDragIndex + 1}?",
                        "Yes", "Cancel"))
                {
                    currentEvent.Use();
                    return;
                }
            }


            MoveElement(startDragIndex, currentDragIndex);
            OnSelectionChanged(currentDragIndex, false);

            if (useFoldout && isDraggedElementExpanded)
            {
                for (int i = currentBeginIndex; i < currentBeginIndex + currenElementCount; i++)
                {
                    if (i == selectedIndex)
                    {
                        SetIsExpanded(i, isDraggedElementExpanded);
                    }
                    else
                    {
                        SetIsExpanded(i, false);
                    }
                }
            }

            currentEvent.Use();
        }

        #endregion

        private void SetCurrentPageManual(int page)
        {
            manualPageNavigation = true;
            currentPage = page;
        }

        private void DrawPagination()
        {
            HandleDrawingBackgroundConfiguration(footerPaginationRect, style.pagination.backgroundConfiguration);

            using (new EditorGUI.DisabledScope(currentPage == 0))
            {
                if (GUI.Button(firstPageButtonRect, style.pagination.firstPageContent, style.pagination.buttonStyle))
                {
                    SetCurrentPageManual(0);
                }
            }

            using (new EditorGUI.DisabledScope(currentPage == 0))
            {
                if (GUI.Button(previousPageButtonRect, style.pagination.previousPageContent,
                        style.pagination.buttonStyle))
                {
                    SetCurrentPageManual(currentPage - 1);
                }
            }

            GUI.Label(paginationLabelRect, (currentPage + 1) + style.SEPARATOR + pagesCount,
                style.pagination.labelStyle);

            using (new EditorGUI.DisabledScope(currentPage == pagesCount - 1))
            {
                if (GUI.Button(nextPageButtonRect, style.pagination.nextPageContent, style.pagination.buttonStyle))
                {
                    SetCurrentPageManual(currentPage + 1);
                }
            }

            using (new EditorGUI.DisabledScope(currentPage == pagesCount - 1))
            {
                if (GUI.Button(lastPageButtonRect, style.pagination.lastPageContent, style.pagination.buttonStyle))
                {
                    SetCurrentPageManual(pagesCount - 1);
                }
            }
        }

        private void DrawFooterButtons()
        {
            rightEdge = footerButtonsRect.xMax - style.footerButtons.marginRight -
                        style.footerButtons.spaceBetweenButtons;
            leftEdge = rightEdge - style.footerButtons.paddingLeft - style.footerButtons.paddingRight;
            leftEdge -= style.footerButtons.buttonsWidth; // space for one button

            if (style.enableFooterRemoveButton || style.enableFooterAddButton)
            {
                leftEdge -= style.footerButtons.buttonsWidth; //space for other button
            }

            buttonsRect.Set(leftEdge, footerButtonsRect.y, rightEdge - leftEdge, footerButtonsRect.height);

            footerButtonRect.Set(leftEdge + style.footerButtons.paddingLeft, buttonsRect.y,
                style.footerButtons.buttonsWidth, style.footerButtons.buttonsHeight);

            HandleDrawingBackgroundConfiguration(buttonsRect, style.footerButtons.backgroundConfiguration);

            if (style.enableFooterAddButton)
            {
                if (addElementWithDropdownCallback != null)
                {
                    if (GUI.Button(footerButtonRect, style.footerButtons.addButtonWithDropdown,
                            style.footerButtons.buttonStyle))
                    {
                        addElementWithDropdownCallback.Invoke();
                    }
                }
                else
                {
                    if (GUI.Button(footerButtonRect, style.footerButtons.addButton, style.footerButtons.buttonStyle))
                    {
                        AddElement();
                    }
                }
                //add button

                footerButtonRect.x += style.footerButtons.buttonsWidth + style.footerButtons.spaceBetweenButtons;
            }

            if (style.enableFooterRemoveButton)
            {
                //remove button
                using (new EditorGUI.DisabledScope((selectedIndex < 0) || (selectedIndex >= ArraySize())))
                {
                    if (GUI.Button(footerButtonRect, style.footerButtons.removeButton, style.footerButtons.buttonStyle))
                    {
                        RemoveElement();
                    }
                }
            }
        }

        private void CollapseElements()
        {
            if (!useFoldout)
            {
                return;
            }

            for (int i = 0; i < ArraySize(); i++)
            {
                if (i != selectedIndex)
                {
                    SetIsExpanded(i, false);
                }
            }
        }

        #region handle callbacks

        private string GetHeaderLabel()
        {
            return getHeaderLabelCallback?.Invoke();
        }

        private void OnSelectionChanged(int index, bool enableCollapse = true)
        {
            manualPageNavigation = false;
            selectedIndex = index;

            if (enableCollapse)
            {
                CollapseElements();
            }

            selectionChangedCallback?.Invoke();
        }

        public void ListChangedCallback()
        {
            listChangedCallback?.Invoke();
        }

        private void AddElement()
        {
            addElementCallback?.Invoke();
            ListChangedCallback();
        }

        private protected void RemoveElement()
        {
            currenElementCount--;
            removeElementCallback?.Invoke();
            ListChangedCallback();
        }

        private void DisplayContextMenu()
        {
            displayContextMenuCallback?.Invoke();
        }

        private void UndoCallback()
        {
            listUndoCallback?.Invoke();
        }

        #endregion
    }
}