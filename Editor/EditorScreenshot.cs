// =================================================================================================
// EditorScreenshots by Pumkin#9524
// https://github.com/rurre/Editor-Screenshot
// Based on an ancient tool somewhere on the Asset Store called Instant Screenshot by Saad Khawaja.
// =================================================================================================

using System;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
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

        readonly Version version = new Version(1, 0);
        const string defaultScreenshotName = "Screenshot_{0}x{1}.png";
        const string resolutionInfoText = "Final screenshot resolution will be {0}x{1}";
        const string kofiLink = "https://ko-fi.com/notpumkin";
        const string githubLink = "https://github.com/rurre/Editor-Screenshot";

        [SerializeField] Texture2D githubIcon;
        [SerializeField] Texture2D kofiIcon;
        [SerializeField] VisualTreeAsset uxmlTree;
        [SerializeField] StyleSheet styleSheet;
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

        ScreenshotCameraType selectedCameraType = ScreenshotCameraType.SceneViewCamera;
        Transform _lastCameraTransform;
        string lastScreenshotPath;

        Label resolutionInfoLabel;

        [MenuItem("Tools/Pumkin/Editor Screenshot", false, 65)]
        public static void ShowWindow()
        {
            EditorWindow window = GetWindow<EditorScreenshot>();
            window.titleContent = new GUIContent("Screenshot");
            window.minSize = new Vector2(335, 360);
        }

        void OnDestroy()
        {
            StopFollowingCamera();
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
            tree = uxmlTree.CloneTree();
            tree.styleSheets.Add(styleSheet);
            rootVisualElement.Add(tree);

            resolutionInfoLabel = tree.Q<Label>("resolutionInfoLabel");
            tree.Q<Label>("versionLabel").text = $"v{version}";

            VisualElement gameCameraContainer = tree.Query<VisualElement>("gameCameraContainer");
            gameCameraContainer.style.display = DisplayStyle.None;

            EnumField camTypeEnum = tree.Query<EnumField>("cameraTypeEnum");
            camTypeEnum.Init(ScreenshotCameraType.SceneViewCamera);
            camTypeEnum.RegisterCallback<ChangeEvent<Enum>>(evt =>
            {
                selectedCameraType = (ScreenshotCameraType)evt.newValue;
                gameCameraContainer.style.display = selectedCameraType == ScreenshotCameraType.GameCamera ? DisplayStyle.Flex : DisplayStyle.None;

                if(FollowSceneCamera)
                {
                    if(selectedCameraType == ScreenshotCameraType.SceneViewCamera)
                        StopFollowingCamera();
                    else
                        FollowSceneCamera = FollowSceneCamera;
                }
            });
            camTypeEnum.value = selectedCameraType;

            ObjectField selectedCameraField = tree.Q<ObjectField>("gameCameraSelector");
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
            resWidthField.value = resolution.x;
            resWidthField.RegisterCallback<ChangeEvent<IntegerField>>(evt => resolution.x = evt.newValue.value);

            IntegerField resHeightField = tree.Q<IntegerField>("resHeightField");
            resHeightField.RegisterCallback<ChangeEvent<IntegerField>>(evt => resolution.y = evt.newValue.value);

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

            VisualElement donateButton = tree.Q("donateButton");
            donateButton.style.backgroundImage = new StyleBackground(kofiIcon);
            donateButton.RegisterCallback<MouseUpEvent>(evt => Application.OpenURL(kofiLink));

            VisualElement githubButton = tree.Q("githubButton");
            githubButton.style.backgroundImage = new StyleBackground(githubIcon);
            githubButton.RegisterCallback<MouseUpEvent>(evt => Application.OpenURL(githubLink));

            UpdateResolutionInfoLabel();
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
                RenderTexture rt = new RenderTexture(resWidth, resHeight, 24);
                cam.targetTexture = rt;

                if(useTransparentBg)
                {
                    cam.backgroundColor = new Color(0, 0, 0, 0);
                    cam.clearFlags = CameraClearFlags.Color;
                }

                if(fixNearClip)
                    cam.nearClipPlane = 0.001f;

                Texture2D screenShot = new Texture2D(resWidth, resHeight, useTransparentBg ? TextureFormat.ARGB32 : TextureFormat.RGB24, false);
                cam.Render();
                RenderTexture.active = rt;
                screenShot.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
                cam.targetTexture = null;
                RenderTexture.active = null;

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