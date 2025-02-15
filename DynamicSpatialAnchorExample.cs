﻿using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.IO;
using UnityEngine;
using Unity.XR.Oculus;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using Oculus.Interaction;

public class SpatialAnchorsExample
{
    [SerializeField] List<Guid> allAnchorsInSessionGuids = new List<Guid>();
    [SerializeField] List<OVRSpatialAnchor> allAnchorsInSession = new List<OVRSpatialAnchor>();
    Action<OVRSpatialAnchor.UnboundAnchor, bool> onLoadAnchor;
    GameObject currentlyDraggedObject;

    #region Startup Stuff

    void Start()
    {
        onLoadAnchor = OnLocalized; // event that will start a method once an anchor has been localized

        StartCoroutine(InitializationProtocol());

    }

    // Start this coroutine in your app's Start funciton or Awake
    IEnumerator InitializationProtocol()
    {
        yield return StartCoroutine(EnsureFilePathExist()); // calling 'yield return StartCoroutine' inside another coroutine means it will wait for this one to finish before continuing. it's super useful
        allAnchorsInSessionGuids = LoadSavedAnchorsUUIDs();

        // Load scene model
        yield return StartCoroutine(LoadSceneModelOnApplicationStart()); // The event that fires after Scene is loaded successfully has been told to trigger the loading of spatial anchors functions below

        yield break;
    }
    IEnumerator EnsureFilePathExist()
    {
        savedAnchorsFilePath = Application.persistentDataPath + "/" + "SavedAnchors" + ".bin";
        if (File.Exists(savedAnchorsFilePath) == false)
        {
            File.Create(savedAnchorsFilePath).Close();
        }
        yield break;
    }
    #endregion

    #region SCENE API 

    IEnumerator LoadSceneModelOnApplicationStart()
    {
        sceneManager.SceneModelLoadedSuccessfully += SceneModelLoadedSuccessfuly;
        sceneManager.NoSceneModelToLoad += NoSceneModelToLoad;

        sceneManager.LoadSceneModel();

        yield break;
    }
    void SceneModelLoadedSuccessfuly()
    {
        scenePermissionWasGranted = true;
        StartCoroutine(LoadSpatialAnchors());

    }
    void NoSceneModelToLoad() // User hasn't set up their Space yet
    {
        sceneManager.SceneCaptureReturnedWithoutError += SuccessfullSceneCapture;
        sceneManager.RequestSceneCapture();
    }
    void UserDeniedPermissionSoLoadSceneFailed()
    {
    }

    void UserPerformedNewRoomSetupWhileApplicationPaused()
    {
        sceneManager.LoadSceneModel();
    }
    void RestartSceneCaptureBecasueOfError()
    {
        sceneManager.RequestSceneCapture();
    }
    void SuccessfullSceneCapture()
    {
        scenePermissionWasGranted = true;
        StartCoroutine(LoadSpatialAnchors());
    }
    #endregion

    #region LOADING SPATIAL ANCHORS
    IEnumerator LoadSpatialAnchors()
    {
        OVRSpatialAnchor.LoadOptions options = new OVRSpatialAnchor.LoadOptions();
        options.StorageLocation = OVRSpace.StorageLocation.Local;
        options.Uuids = allAnchorsInSessionGuids;

        if (allAnchorsInSessionGuids != null && allAnchorsInSessionGuids.Count > 0)
        {
            OVRSpatialAnchor.LoadUnboundAnchors(options, anchors =>
            {
                if (anchors == null)
                {
                    return;
                }
                foreach (var anchor in anchors)
                {
                    if (anchor.Localized)
                    {
                        OnLocalized(anchor, true);
                    }
                    else if (!anchor.Localizing)
                    {
                        anchor.Localize(onLoadAnchor);
                    }
                }
            });
        }

        yield break;
    }

    void OnLocalized(OVRSpatialAnchor.UnboundAnchor unboundAnchor, bool success)
    {
        if (!success) { return; }

        var pose = unboundAnchor.Pose;
        // INSTANTIATE A PREFAB
        GameObject spatialAnchorGO = Instantiate(yourPrefab, pose.position, pose.rotation);
        spatialAnchorGO.transform.localScale = new Vector3(.0001f, .0001f, .0001f);
        spatialAnchorGO.transform.DOScale(Vector3.one, .6f);
        OVRSpatialAnchor prefabAnchor = spatialAnchorGO.GetComponent<OVRSpatialAnchor>();
        unboundAnchor.BindTo(prefabAnchor);

        allAnchorsInSession.Add(prefabAnchor); // add the spatial anchor i either addcomponent or it's already on a prefab i instantiate
    }
    #endregion

    #region SAVING NEWLY CREATED SPATIAL ANCHORS

    IEnumerator SaveTheAnchor()
    {
        yield return StartCoroutine(SaveAllActiveAnchorUUIDs()); // I just like to call this obsessively to make sure everything is saved up-to-date. It didn't create frame drops for me

        CreateAndSaveSpatialAnchor(prefabToCreateAnchorOn);
    }


    async void CreateAndSaveSpatialAnchor(GameObject prefabToCreateAnchorOn) // Saving Anchors created at runtime 
    {
        // If the prefab already has a spatial anchor for some reason 
        if (prefabToCreateAnchorOn.TryGetComponent<OVRSpatialAnchor>(out OVRSpatialAnchor existingAnchor))
        {
            // Get rid (destroy) that old anchor in preparation to make a new one. First we remove it from our lists, then below we call Meta's Erase() and then destroy it's instance via Unity "Destroy(" 
            allAnchorsInSession.Remove(existingAnchor);
            allAnchorsInSessionGuids.Remove(existingAnchor.Uuid);

            existingAnchor.Erase((anchorr, successs) =>
            {
                if (successs)
                {
                    Destroy(existingAnchor);
                }
            });

            while (existingAnchor != null)
            {
                await Task.Yield(); // wait til it's destroyed officially (usually next frame)
            }
        }

        OVRSpatialAnchor anchorToSave = prefabToCreateAnchorOn.AddComponent<OVRSpatialAnchor>(); // create new one 

        while (!anchorToSave.Created && !anchorToSave.Localized) // keep checking for a valid and localized spatial anchor state
        {
            await Task.Yield();
        }

        var result = await anchorToSave.SaveAsync(); // Save the new anchor to Meta

        // If save is successfull, save to all our lists. 
        if (result)
        {
            allAnchorsInSession.Add(anchorToSave);
            allAnchorsInSessionGuids.Add(anchorToSave.Uuid);

            // Save our the new anchor's uuid's to our serialization list
            StartCoroutine(SaveAllActiveAnchorUUIDs());
        }
        else
        {
            Debug.Log("Save Failed");
        }
    }
    #endregion

    #region ERASING AND RE-CREATING SPATIAL ANCHORS ON GRAB AND MOVE OBJECT
    // You gotta delete the old anchor on grab, otherwise it won't be move-able. I think you can maybe get around this by moving a child of the anchor, but you need to double check. You would still need to delete and re-add the parents anchor though anyway depending what you're making
    public void OnHandGrab(GameObject objectJustGrabbed)
    {
        currentlyDraggedObject = objectJustGrabbed;

        // You may want to disable the hand grab interactable in the object just for safety to avoid duplicate method calling
        HandGrabInteractable handGrabComponent = prefabJustMoved.GetComponentInChildren<HandGrabInteractable>();
        handGrabComponent.enabled = false;

        // Delete it so you can drag it
        // PER META DOCS - "Anchors cannot be moved. If the content must be moved, delete the old anchor and create a new one."
        OVRSpatialAnchor oldAnchor = prefabJustMoved.GetComponent<OVRSpatialAnchor>();
        Guid oldAnchorsUUID = oldAnchor.Uuid;

        allAnchorsInSession.Remove(oldAnchor);
        allAnchorsInSessionGuids.Remove(oldAnchor.Uuid);

        oldAnchor.Erase((anchorr, successs) =>
        {
            if (successs)
            {
                Destroy(oldAnchor);
            }
        });

        while (oldAnchor != null)
        {
            yield return new WaitForEndOfFrame();
        }
    }
    public void OnHandRelease()
    {
        if (currentlyDraggedObject)
        {
            StartCoroutine(UpdateSpatialAnchorInfoWhenUserMovesAndReleasesPrefab(currentlyDraggedObject.transform.position, currentlyDraggedObject));

            // re-enable the hand grab interactable so you can drag again 
            HandGrabInteractable handGrabComponent = prefabJustMoved.GetComponentInChildren<HandGrabInteractable>();
            handGrabComponent.enabled = true;

            currentlyDraggedObject = null;
        }
    }
    IEnumerator UpdateSpatialAnchorInfoWhenUserMovesAndReleasesPrefab(Vector3 positionJustMovedTo, GameObject prefabJustMoved)
    {
        OVRSpatialAnchor newAnchor = prefabJustMoved.AddComponent<OVRSpatialAnchor>();

        while (!newAnchor.Created && !newAnchor.Localized) // keep checking for a valid and localized spatial anchor state
        {
            yield return new WaitForEndOfFrame();
        }

        newAnchor.Save((anchor, success) =>
        {
            if (success)
            {
                allAnchorsInSession.Add(newAnchor);
                allAnchorsInSessionGuids.Add(newAnchor.Uuid);

                StartCoroutine(SaveAllActiveAnchorUUIDs()); // Serialize the list again 
            }
        });

        yield break;
    }

    #endregion

    #region SERIALIZATION / DESERIALIZATION (REPLACE ODIN WITH SERIALIZATION TOOL OF YOUR CHOICE)
    //reads all your saved anchor uuids from disk. YOu gotta do this before calling LoadSpatialAnchors
    List<Guid> LoadSavedAnchorsUUIDs()
    {
        byte[] rawBytes = File.ReadAllBytes(savedAnchorsFilePath);
        List<Guid> fromFolder = Sirenix.Serialization.SerializationUtility.DeserializeValue<List<Guid>>(rawBytes, DataFormat.Binary);
        if (fromFolder != null && fromFolder.Count > 0) { return fromFolder; }
        else { return new List<Guid>(); }
    }

    // Call this Coroutine whenever you want to save all your anchors
    IEnumerator SaveAllActiveAnchorUUIDs()
    {
        byte[] storageBytes = Sirenix.Serialization.SerializationUtility.SerializeValue<List<Guid>>(allAnchorsInSessionGuids, DataFormat.Binary);
        File.WriteAllBytes(savedAnchorsFilePath, storageBytes);
        yield break;
    }
    #endregion

}
