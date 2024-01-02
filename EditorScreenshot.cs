// EditorScreenshots by Pumkin#9524
// https://github.com/rurre/Editor-Screenshot
// Based on an ancient tool somewhere on the Asset Store called Instant Screenshot by Saad Khawaja.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pumkin.EditorScreenshot
{
    [Serializable]
    public class EditorScreenshot : EditorWindow
    {
        enum ScreenshotCameraType
        {
            SceneViewCamera,
            GameCamera
        };

        string version = "Unknown";
        const string defaultScreenshotName = "Screenshot_{0}x{1}.png";
        const string resolutionInfoText = "Final screenshot resolution will be {0}x{1}";
        const string kofiLink = "https://ko-fi.com/notpumkin";
        const string githubLink = "https://github.com/rurre/Editor-Screenshot";
        const string editorPrefsSettingsKey = "PumkinsEditorScreenshotSettings";

        Texture2D githubIcon;
        Texture2D kofiIcon;
        VisualTreeAsset uxmlTree;
        StyleSheet styleSheet;
        VisualElement tree;

        Camera TargetCamera
        {
            get
            {
                if(selectedCameraType == ScreenshotCameraType.GameCamera)
                    return _selectedCamera;
                return SceneView.lastActiveSceneView ? SceneView.lastActiveSceneView.camera : null;
            }
            set
            {
                _selectedCamera = value;
                _lastCameraTransform = value ? value.transform : null;
                if(!value)
                    StopFollowingCamera();
                else
                    FollowSceneCamera = FollowSceneCamera;
            }
        }

        bool FollowSceneCamera
        {
            get => _followSceneCamera;
            set
            {
                _followSceneCamera = value;
                StopFollowingCamera();
                if(value)
                    StartFollowingCamera();
            }
        }

        Camera _selectedCamera;
        [SerializeField] bool _followSceneCamera = false;
        [SerializeField] bool fixNearClip = true;
        [SerializeField] bool useTransparentBg = true;
        [SerializeField] bool openScreenshotAfterSaving = true;
        [SerializeField] string savePath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        [SerializeField] Vector2Int resolution = new Vector2Int(1920, 1080);
        [SerializeField] int resolutionMultiplier = 1;

        [SerializeField] ScreenshotCameraType selectedCameraType = ScreenshotCameraType.SceneViewCamera;
        Transform _lastCameraTransform;
        string lastScreenshotPath;

        [SerializeField] bool selectCreatedCamera = false;

        Label resolutionInfoLabel;
        ObjectField selectedCameraField;
        EnumField cameraTypeEnumField;

		static EditorWindow window;

        string ProjectPath
        {
            get
            {
                if(string.IsNullOrWhiteSpace(_projectPath))
                {
                    string dataPath = Application.dataPath;
                    _projectPath = dataPath.Substring(0, dataPath.LastIndexOf("Assets"));
                }
                return _projectPath;
            }
        }
        string _projectPath;

        [MenuItem("Tools/Pumkin/Editor Screenshot", false, 65)]
        public static void ShowWindow()
        {
            window = GetWindow<EditorScreenshot>();
            window.titleContent = new GUIContent("Screenshot");
            window.minSize = new Vector2(335, 360);
        }

        void Awake()
        {
            LoadSettings();
        }

        void OnDestroy()
        {
            StopFollowingCamera();
            SaveSettings();
        }

        class PackageJson
        {
            public string version;
        }

        void LoadSettings()
        {
            try
            {
                string json = EditorPrefs.GetString(editorPrefsSettingsKey, null);
                if(!string.IsNullOrWhiteSpace(json))
                    JsonUtility.FromJsonOverwrite(json, this);
            }
            catch(Exception ex)
            {
                Debug.LogWarning(FormatLogMessage("Failed to load window settings:"));
                Debug.LogException(ex);
            }

            // Get version from package.json
            string packagePath = $"{ProjectPath}Packages/io.github.rurre.editor-screenshot/package.json";
            if(File.Exists(packagePath))
            {
                string json = File.ReadAllText(packagePath);
                var jsonInstance = JsonUtility.FromJson<PackageJson>(json);
                if(jsonInstance != null)
                    version = jsonInstance.version;
            }
        }

        void SaveSettings()
        {
            try
            {
                string json = JsonUtility.ToJson(this);
                EditorPrefs.SetString(editorPrefsSettingsKey, json);
            }
            catch(Exception ex)
            {
                Debug.LogError(FormatLogMessage("Failed to save window settings."));
                Debug.LogException(ex);
            }
        }

        void StartFollowingCamera() => EditorApplication.update += CameraToSceneCamera;
        void StopFollowingCamera() => EditorApplication.update -= CameraToSceneCamera;

        void CameraToSceneCamera()
        {
            _lastCameraTransform = SceneView.lastActiveSceneView ? SceneView.lastActiveSceneView.camera.transform : null;
            if(_lastCameraTransform && TargetCamera)
                TargetCamera.transform.SetPositionAndRotation(_lastCameraTransform.localPosition, _lastCameraTransform.localRotation);
        }

        void CreateGUI()
        {
            uxmlTree = Resources.Load<VisualTreeAsset>("Pumkin/EditorScreenshot/EditorScreenshot");
            styleSheet = Resources.Load<StyleSheet>("Pumkin/EditorScreenshot/EditorScreenshot");

            tree = uxmlTree.CloneTree();
            tree.styleSheets.Add(styleSheet);
            rootVisualElement.Add(tree);

            resolutionInfoLabel = tree.Q<Label>("resolutionInfoLabel");
            tree.Q<Label>("versionLabel").text = $"v{version}";

            VisualElement gameCameraContainer = tree.Query<VisualElement>("gameCameraContainer");
            gameCameraContainer.style.display = DisplayStyle.None;

            VisualElement sceneCameraContainer = tree.Query<VisualElement>("sceneCameraContainer");
            sceneCameraContainer.style.display = DisplayStyle.Flex;

            Toggle selectCreatedCameraToggle = tree.Q<Toggle>("selectCreatedCameraToggle");
            selectCreatedCameraToggle.RegisterCallback<ChangeEvent<bool>>(evt => selectCreatedCamera = evt.newValue);
            selectCreatedCameraToggle.value = selectCreatedCamera;

            tree.Q("cameraFromSceneButton").RegisterCallback<MouseUpEvent>(evt => CreateCameraFromSceneView());

            cameraTypeEnumField = tree.Query<EnumField>("cameraTypeEnum");
            cameraTypeEnumField.Init(ScreenshotCameraType.SceneViewCamera);
            cameraTypeEnumField.RegisterCallback<ChangeEvent<Enum>>(evt =>
            {
                selectedCameraType = (ScreenshotCameraType)evt.newValue;
                gameCameraContainer.style.display = selectedCameraType == ScreenshotCameraType.GameCamera ? DisplayStyle.Flex : DisplayStyle.None;
                sceneCameraContainer.style.display = selectedCameraType == ScreenshotCameraType.SceneViewCamera ? DisplayStyle.Flex : DisplayStyle.None;

                if(FollowSceneCamera)
                {
                    if(selectedCameraType == ScreenshotCameraType.SceneViewCamera)
                        StopFollowingCamera();
                    else
                        FollowSceneCamera = FollowSceneCamera;
                }
            });
            cameraTypeEnumField.value = selectedCameraType;

            selectedCameraField = tree.Q<ObjectField>("gameCameraSelector");
            selectedCameraField.allowSceneObjects = true;
            selectedCameraField.objectType = typeof(Camera);
            selectedCameraField.RegisterCallback<ChangeEvent<UnityEngine.Object>>(evt =>
            {
                var newCam = evt.newValue as Camera;
                TargetCamera = newCam;
                if(newCam)
                    _lastCameraTransform = newCam.transform;
            });
            TargetCamera = null;

            tree.Q<Button>("gameCameraFromSelection").RegisterCallback<MouseUpEvent>(evt =>
            {
                GameObject selection = Selection.activeObject as GameObject;
                if(selection && selection.TryGetComponent(out Camera camera))
                    selectedCameraField.value = camera;
                else
                    selectedCameraField.value = Camera.main;
            });

            Toggle followSceneCameraToggle = tree.Q<Toggle>("followSceneCameraToggle");
            followSceneCameraToggle.value = FollowSceneCamera;
            followSceneCameraToggle.RegisterCallback<ChangeEvent<bool>>(evt => FollowSceneCamera = evt.newValue);

            IntegerField resWidthField = tree.Q<IntegerField>("resWidthField");
            resWidthField.RegisterCallback<ChangeEvent<int>>(evt =>
            {
                resolution.x = evt.newValue;
                UpdateResolutionInfoLabel();

            });
            resWidthField.value = resolution.x;

            IntegerField resHeightField = tree.Q<IntegerField>("resHeightField");
            resHeightField.RegisterCallback<ChangeEvent<int>>(evt =>
            {
                resolution.y = evt.newValue;
                UpdateResolutionInfoLabel();
            });
            resHeightField.value = resolution.y;

            // BEGIN Resolution Presets Dropdown

            // Add preset Vector2Ints for each Resolution Preset
            List<Vector2Int> presetResolutions = new List<Vector2Int>() {
                new Vector2Int(1200, 900), // VRC Avatar and World Thumbnail
                new Vector2Int(720, 480), // 480p SD
                new Vector2Int(1280, 720), // 720p HD
                new Vector2Int(1920, 1080), // 1080p FHD
                new Vector2Int(2560, 1440), // 1440p QHD
                new Vector2Int(3840, 2160), // 4K UHD
                new Vector2Int(7680, 4320), // 8K UHD
                new Vector2Int(-1, -1) // This is a Custom Resolution
            };

            // Replace Labels on the UI to these Strings (MUST MATCH EXACT ORDER AS ABOVE!!)
            List<string> presetLabels = new List<string>() {
                "VRC Thumbnail",
                "480p",
                "720p",
                "1080p",
                "1440p",
                "4K",
                "8K",
                "Custom"
            };
            
            // Create VisualElement for the Preset Dropdown
            VisualElement resolutionContainer = tree.Q<VisualElement>("resolutionContainer");

            // Create PopupField
            PopupField<Vector2Int> resolutionDropdown = new PopupField<Vector2Int>("Presets", presetResolutions, 0,
            formatListItemCallback: (Vector2Int res) => {
                int index = presetResolutions.IndexOf(res);
                return index >= 0 ? presetLabels[index] : "Custom";
            },
            formatSelectedValueCallback: (Vector2Int res) => {
                int index = presetResolutions.IndexOf(res);
                return index >= 0 ? presetLabels[index] : "Custom";
            });
            
            // Add change listeners to the Integer Fields
            resWidthField.RegisterValueChangedCallback(evt => {
                if (!presetResolutions.Any(res => res.x == evt.newValue && res.y == resHeightField.value)) {
                    resolutionDropdown.value = new Vector2Int(-1, -1); // Set to Custom
                }
            });
            resHeightField.RegisterValueChangedCallback(evt => {
                if (!presetResolutions.Any(res => res.y == evt.newValue && res.x == resWidthField.value)) {
                    resolutionDropdown.value = new Vector2Int(-1, -1); // Set to Custom
                }
            });
            
            // Dropdown Change Event Handler
            resolutionDropdown.RegisterValueChangedCallback(evt => {
                if (evt.newValue.x == -1 && evt.newValue.y == -1) {
                    return; // Don't update the Integer Fields if "Custom" is selected
                }
                resolution = evt.newValue;
                resWidthField.value = resolution.x;
                resHeightField.value = resolution.y;
                UpdateResolutionInfoLabel();
            });
            
            // Add Dropdown to the resolutionContainer
            resolutionContainer.Add(resolutionDropdown);
            
            // END Resolution Presets Dropdown

            IntegerField multiplierInt = tree.Q<IntegerField>("multiplierInt");
            SliderInt multiplierSlider = tree.Q<SliderInt>("multiplierSlider");

            multiplierInt.RegisterCallback<ChangeEvent<int>>(evt =>
            {
                int value = Mathf.Clamp(evt.newValue, 0, 10);
                resolutionMultiplier = value;
                multiplierSlider.SetValueWithoutNotify(value);
                if(evt.newValue > 10)
                    multiplierInt.SetValueWithoutNotify(value);

                UpdateResolutionInfoLabel();
            });

            multiplierSlider.RegisterCallback<ChangeEvent<int>>(evt =>
            {
                int value = Mathf.Clamp(evt.newValue, 0, 10);
                resolutionMultiplier = value;
                multiplierInt.SetValueWithoutNotify(value);

                UpdateResolutionInfoLabel();
            });
            multiplierInt.value = resolutionMultiplier;
            multiplierSlider.value = multiplierInt.value;

            Toggle transparentBackgroundToggle = tree.Q<Toggle>("transparentBackgroundToggle");
            transparentBackgroundToggle.RegisterCallback<ChangeEvent<bool>>(evt => useTransparentBg = evt.newValue);

            Toggle fixNearClipToggle = tree.Q<Toggle>("fixNearClipToggle");
            fixNearClipToggle.RegisterCallback<ChangeEvent<bool>>(evt => fixNearClip = evt.newValue);

            TextField filePathField = tree.Q<TextField>("filePathField");
            filePathField.RegisterCallback<ChangeEvent<string>>(evt =>
            {
                savePath = evt.newValue;
            });
            filePathField.value = savePath;

            tree.Q("browseButton").RegisterCallback<MouseUpEvent>(evt =>
            {
                string path = EditorUtility.OpenFolderPanel("Save screenshots to", savePath, null);
                savePath = string.IsNullOrWhiteSpace(path) ? savePath : path;
                filePathField.value = savePath;
            });

            Toggle openScreenshotToggle = tree.Q<Toggle>("openScreenshotAfterSavingToggle");
            openScreenshotToggle.value = openScreenshotAfterSaving;
            openScreenshotToggle.RegisterCallback<ChangeEvent<bool>>(evt => openScreenshotAfterSaving = evt.newValue);

            tree.Q("screenshotButton").RegisterCallback<MouseUpEvent>(evt => TakeScreenshot());
            tree.Q("openLastButton").RegisterCallback<MouseUpEvent>(evt => OpenLastScreenshot());
            tree.Q("openFolderButton").RegisterCallback<MouseUpEvent>(evt => OpenSaveFolder());

            kofiIcon = Resources.Load<Texture2D>("Pumkin/EditorScreenshot/logo_kofi");
            VisualElement donateButton = tree.Q("donateButton");
            donateButton.style.backgroundImage = new StyleBackground(kofiIcon);
            donateButton.RegisterCallback<MouseUpEvent>(evt => Application.OpenURL(kofiLink));

            githubIcon = Resources.Load<Texture2D>("Pumkin/EditorScreenshot/logo_github");
            VisualElement githubButton = tree.Q("githubButton");
            githubButton.style.backgroundImage = new StyleBackground(githubIcon);
            githubButton.RegisterCallback<MouseUpEvent>(evt => Application.OpenURL(githubLink));

            UpdateResolutionInfoLabel();
        }

        void CreateCameraFromSceneView()
        {
            if(!SceneView.lastActiveSceneView && !SceneView.lastActiveSceneView.camera)
            {
                Debug.LogError(FormatLogMessage("Failed to create camera from scene view."));
                return;
            }

            var sceneCam = SceneView.lastActiveSceneView.camera;

            var camObj = new GameObject("Camera");
            camObj.transform.SetPositionAndRotation(sceneCam.transform.position, sceneCam.transform.rotation);
            ComponentUtility.CopyComponent(SceneView.lastActiveSceneView.camera);
            ComponentUtility.PasteComponentAsNew(camObj);

            var cam = camObj.GetComponent<Camera>();
            cam.enabled = true;
            cam.targetTexture = null;

            if(selectCreatedCamera)
            {
                cameraTypeEnumField.value = ScreenshotCameraType.GameCamera;
                selectedCameraField.value = camObj.GetComponent<Camera>();
            }
        }

        void UpdateResolutionInfoLabel()
        {
            resolutionInfoLabel.text = string.Format(resolutionInfoText, resolution.x * resolutionMultiplier, resolution.y * resolutionMultiplier);
        }

        void TakeScreenshot()
        {
            if(!TargetCamera)
            {
                Debug.LogWarning("Please select a camera.");
                return;
            }

            int resWidth = resolution.x * resolutionMultiplier;
            int resHeight = resolution.y * resolutionMultiplier;

            string screenshotName = GenerateScreenshotName();
            string screenshotPath = $"{savePath}\\{screenshotName}";

            string logMsg = $"Attempting to take screenshot <b>{screenshotName}</b> and save it to <b>{screenshotPath}</b> - ";

            RenderTexture oldRt = TargetCamera.targetTexture;
            Color oldColor = TargetCamera.backgroundColor;
            CameraClearFlags oldFlags = TargetCamera.clearFlags;
            float oldNearClip = TargetCamera.nearClipPlane;
            RenderTexture oldActiveRT = RenderTexture.active;
            bool success;

            try
            {
                Camera cam = TargetCamera;
                RenderTexture rtHDR = new RenderTexture(resWidth, resHeight, 24, UnityEngine.Experimental.Rendering.DefaultFormat.HDR);
                cam.targetTexture = rtHDR;

                if(useTransparentBg)
                {
                    cam.backgroundColor = new Color(0, 0, 0, 0);
                    cam.clearFlags = CameraClearFlags.Color;
                }

                if(fixNearClip)
                    cam.nearClipPlane = 0.001f;

                cam.Render();

                RenderTexture rtLDR = new RenderTexture(resWidth, resHeight, 24, UnityEngine.Experimental.Rendering.DefaultFormat.LDR);
                Graphics.Blit(rtHDR, rtLDR);
                RenderTexture.active = rtLDR;

                Texture2D screenShot = new Texture2D(resWidth, resHeight, useTransparentBg ? TextureFormat.ARGB32 : TextureFormat.RGB24, false);
                screenShot.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);

                cam.targetTexture = null;
                RenderTexture.active = null;

                if(!Directory.Exists(savePath))
                    Directory.CreateDirectory(savePath);

                File.WriteAllBytes(screenshotPath, screenShot.EncodeToPNG());
                lastScreenshotPath = screenshotPath;

                success = true;
                logMsg += "<b>Success!</b>";

                if(openScreenshotAfterSaving)
                    Application.OpenURL(lastScreenshotPath);
            }
            catch(Exception ex)
            {
                logMsg += $"<b>Failed:</b> {ex.Message}";
                success = false;
            }
            finally
            {
                TargetCamera.targetTexture = oldRt;
                RenderTexture.active = oldActiveRT;
                if(useTransparentBg)
                {
                    TargetCamera.clearFlags = oldFlags;
                    TargetCamera.backgroundColor = oldColor;
                }

                if(fixNearClip)
                    TargetCamera.nearClipPlane = oldNearClip;
            }
            if(success)
                Debug.Log(FormatLogMessage(logMsg));
            else
                Debug.LogError(FormatLogMessage(logMsg));
        }

        void OpenSaveFolder()
        {
            if(Directory.Exists(savePath))
                Application.OpenURL("file:///" + savePath);
            else if(string.IsNullOrWhiteSpace(savePath))
                Debug.LogWarning(FormatLogMessage("The save path is empty."));
            else
                Debug.LogWarning(FormatLogMessage("Invalid save folder. Can't open " + savePath));
        }

        void OpenLastScreenshot()
        {
            if(string.IsNullOrWhiteSpace(lastScreenshotPath))
                Debug.LogWarning(FormatLogMessage("No screenshots have been taken."));
            else if(File.Exists(lastScreenshotPath))
                Application.OpenURL("file:///" + lastScreenshotPath);
            else
                Debug.LogWarning(FormatLogMessage("Screenshot doesn't exist at path: " + lastScreenshotPath));
        }

        string FormatLogMessage(string message)
        {
            return $"<b>Editor Screenshot:</b> {message}";
        }

        string GenerateScreenshotName()
        {
            string fileName = string.Format(defaultScreenshotName, resolution.x * resolutionMultiplier, resolution.y * resolutionMultiplier);
            return GetUniqueFileName(fileName, savePath);
        }

        string GetUniqueFileName(string fileName, string folderPath)
        {
            string pathAndFileName = Path.Combine(folderPath, fileName);
            string validatedName = fileName;
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(pathAndFileName);
            string ext = Path.GetExtension(pathAndFileName);
            int count = 1;
            while(File.Exists(Path.Combine(folderPath, validatedName)))
            {
                validatedName = string.Format("{0}_{1}{2}",
                    fileNameWithoutExt,
                    count++,
                    ext);
            }
            return validatedName;
        }
    }
}
