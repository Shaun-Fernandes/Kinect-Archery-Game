using UnityEngine;
using System.Collections;
using System.Text;

public class HandObjectChecker : MonoBehaviour 
{
	[Tooltip("Index of the player, tracked by this component. 0 means the 1st player, 1 - the 2nd one, 2 - the 3rd one, etc.")]
	public int playerIndex = 0;

	[Tooltip("Maximum distance in horizontal & vertical directions from the hand center, in meters, to be tracked for an object.")]
	public float maxHvDistanceToHand = 0.1f;

	[Tooltip("Maximum distance in depth from the hand center, in meters, to be tracked for an object.")]
	public float maxZDistanceToJoint = 0.05f;

	[Tooltip("Minimum fill ratio of the tracked depth area, to be considered as a valid object.")]
	public float fillThreshold = 0.5f;

	[Tooltip("Whether to draw the hand-status rectangles.")]
	public bool drawHandRectangles = false;

	[Tooltip("UI-Text to display status messages.")]
	public UnityEngine.UI.Text statusText;

	private long trackedUserId;
	private byte userBodyIndex;

	private KinectManager manager;
	private KinectInterop.SensorData sensorData;
	private long lastDepthFrameTime;

	private Vector2 dposHandLeft = Vector2.zero;
	private Vector2 dposHandRight = Vector2.zero;

	private Vector2 depthMinMaxHL = Vector2.zero;
	private Vector2 depthMinMaxHR = Vector2.zero;

	private Rect rectObjectHandLeft = new Rect();
	private Rect rectObjectHandRight = new Rect();

	private float fillRatioLeftHand = 0f;
	private float fillRatioRightHand = 0f;

//	private Vector3 sizeObjectHandLeft = Vector3.zero;
//	private Vector3 sizeObjectHandRight = Vector3.zero;


	void Start () 
	{
		manager = KinectManager.Instance;
		sensorData = manager ? manager.GetSensorData() : null;
	}
	
	void Update () 
	{
		if(!manager || !manager.IsInitialized())
			return;

		// get required player
		long userId = manager.GetUserIdByIndex (playerIndex);

		if (trackedUserId != userId) 
		{
			// user lost
			trackedUserId = 0;
		}

		if(trackedUserId == 0 && userId != 0)
		{
			// new user found
			trackedUserId = userId;
		}

		if (trackedUserId != 0 && sensorData.bodyIndexImage != null && sensorData.depthImage != null &&
		    sensorData.lastDepthFrameTime != lastDepthFrameTime) 
		{
			lastDepthFrameTime = sensorData.lastDepthFrameTime;
			userBodyIndex = (byte)manager.GetBodyIndexByUserId(trackedUserId);

			TrackDepthAroundJoint((int)KinectInterop.JointType.HandLeft, ref dposHandLeft, ref rectObjectHandLeft, ref depthMinMaxHL, ref fillRatioLeftHand);
			TrackDepthAroundJoint((int)KinectInterop.JointType.HandRight, ref dposHandRight, ref rectObjectHandRight, ref depthMinMaxHR, ref fillRatioRightHand);

//			CalculateObjectSize(dposHandLeft, rectObjectHandLeft, depthMinMaxHL, ref sizeObjectHandLeft);
//			CalculateObjectSize(dposHandRight, rectObjectHandRight, depthMinMaxHR, ref sizeObjectHandRight);

			if (drawHandRectangles) 
			{
				Texture2D texDepth = manager.GetUsersLblTex2D();

				bool bRectDrawn = false;
				if (rectObjectHandLeft.width != 0f && rectObjectHandLeft.height != 0f && dposHandLeft != Vector2.zero) 
				{
					KinectInterop.DrawRect(texDepth, rectObjectHandLeft, fillRatioLeftHand > fillThreshold ? Color.green : Color.yellow);
					bRectDrawn = true;
				}

				if (rectObjectHandRight.width != 0f && rectObjectHandRight.height != 0f && dposHandRight != Vector2.zero) 
				{
					KinectInterop.DrawRect(texDepth, rectObjectHandRight, fillRatioRightHand > fillThreshold ? Color.green : Color.yellow);
					bRectDrawn = true;
				}

				if (bRectDrawn) 
				{
					texDepth.Apply();
				}
			}

			StringBuilder sbStatusText = new StringBuilder();

			sbStatusText.AppendFormat("LH-Fill: {0:F1}%", fillRatioLeftHand * 100f);
			if (fillRatioLeftHand > fillThreshold)
				sbStatusText.Append(" - Object Found");
			sbStatusText.AppendLine();

			sbStatusText.AppendFormat("RF-Fill: {0:F1}%", fillRatioRightHand * 100f);
			if (fillRatioRightHand > fillThreshold)
				sbStatusText.Append(" - Object Found");
			sbStatusText.AppendLine();

//			if (!float.IsNaN(sizeObjectHandLeft.x) && !float.IsNaN(sizeObjectHandLeft.y) && !float.IsNaN(sizeObjectHandLeft.z)) 
//			{
//				sbStatusText.AppendFormat("L: ({0:F2}, {1:F2}, {2:F2}), {3:F1}%\n", sizeObjectHandLeft.x, sizeObjectHandLeft.y, sizeObjectHandLeft.z, fillRatioLeftHand * 100f);
//			}
//
//			if (!float.IsNaN(sizeObjectHandRight.x) && !float.IsNaN(sizeObjectHandRight.y) && !float.IsNaN(sizeObjectHandRight.z)) 
//			{
//				sbStatusText.AppendFormat("R: ({0:F2}, {1:F2}, {2:F2}), {3:F1}%\n", sizeObjectHandRight.x, sizeObjectHandRight.y, sizeObjectHandRight.z, fillRatioRightHand * 100f);
//			}
//
//			if (fillRatioLeftHand > fillThreshold)
//				sbStatusText.Append("Found object in the left hand.\n");
//			if (fillRatioRightHand > fillThreshold)
//				sbStatusText.Append("Found object in the right hand.\n");

			if (statusText) 
			{
				statusText.text = sbStatusText.ToString();
			}
		}

	}


	// checks for object around the joint, according to the given threshold values
	private bool TrackDepthAroundJoint(int iJoint, ref Vector2 dposJoint, ref Rect rectObject, ref Vector2 depthMinMax, ref float fillRatio)
	{
		// clear results
		dposJoint = Vector2.zero;
		rectObject.width = 0f;
		rectObject.height = 0f;
		fillRatio = 0f;

		if(!manager.IsJointTracked(trackedUserId, iJoint))
			return false;

		// space & depth pos
		Vector3 jointSpacePos = manager.GetJointKinectPosition(trackedUserId, iJoint);
		Vector2 jointDepthPos = manager.MapSpacePointToDepthCoords(jointSpacePos);
		dposJoint = jointDepthPos;

		if(jointSpacePos == Vector3.zero || jointDepthPos == Vector2.zero)
			return false;

		// depth width and height
		int depthWidth = sensorData.depthImageWidth;
		int depthHeight = sensorData.depthImageHeight;

		// left & right
		Vector3 spaceLeft = jointSpacePos - new Vector3(maxHvDistanceToHand, 0f, 0f);
		Vector3 spaceRight = jointSpacePos + new Vector3(maxHvDistanceToHand, 0f, 0f);

		Vector2 depthLeft = manager.MapSpacePointToDepthCoords(spaceLeft);
		if (depthLeft == Vector2.zero) depthLeft = new Vector2(0f, jointDepthPos.y);

		Vector2 depthRight = manager.MapSpacePointToDepthCoords(spaceRight);
		if (depthRight == Vector2.zero) depthRight = new Vector2(depthWidth, jointDepthPos.y);

		// up and down
		Vector3 spaceTop = jointSpacePos + new Vector3(0f, maxHvDistanceToHand, 0f);
		Vector3 spaceBottom = jointSpacePos - new Vector3(0f, maxHvDistanceToHand, 0f);

		Vector2 depthTop = manager.MapSpacePointToDepthCoords(spaceTop);
		if (depthTop == Vector2.zero) depthTop = new Vector2(jointDepthPos.x, 0f);

		Vector2 depthBottom = manager.MapSpacePointToDepthCoords(spaceBottom);
		if (depthBottom == Vector2.zero) depthBottom = new Vector2(jointDepthPos.x, depthHeight);

		// depth
		//ushort jointDepth = manager.GetDepthForPixel((int)jointDepthPos.x, (int)jointDepthPos.y);
		ushort depthMin = (ushort)((jointSpacePos.z - maxZDistanceToJoint) * 1000f);
		ushort depthMax = (ushort)((jointSpacePos.z + maxZDistanceToJoint) * 1000f);

		// calculate the depth rectangle around joint
		FindJointDepthRect((int)depthLeft.x, (int)depthTop.y, (int)depthRight.x, (int)depthBottom.y, depthMin, depthMax, 
			userBodyIndex, ref rectObject, ref depthMinMax, ref fillRatio);

		return true;
	}

	// calculates depth rectangle around the joint belonging to the user, as well as near and far depth
	private void FindJointDepthRect(int minX, int minY, int maxX, int maxY, ushort minDepth, ushort maxDepth, 
									byte userIndex, ref Rect rectResult, ref Vector2 depthMinMax, ref float fillRatio)
	{
		rectResult.x = rectResult.y = rectResult.width = rectResult.height = 0;

		int rectX1 = maxX;
		int rectX2 = minX;
		int rectY1 = maxY;
		int rectY2 = minY;

		ushort nearDepth = maxDepth;
		ushort farDepth = minDepth;

		// start index
		int depthWidth = sensorData.depthImageWidth;
		int depthLength = sensorData.depthImageWidth * sensorData.depthImageHeight;

		int rowIndex = minY * depthWidth + minX;
		int fillCount = 0;

		for (int y = minY; y < maxY; y++) 
		{
			int index = rowIndex;

			for (int x = minX; x < maxX; x++) 
			{
				byte bodyIndex = index >= 0 && index < depthLength ? sensorData.bodyIndexImage[index] : (byte)0;
				ushort depth = index >= 0 && index < depthLength ? sensorData.depthImage[index] : (ushort)0;

				if(bodyIndex == userIndex && depth != 0 && (depth >= minDepth && depth <= maxDepth)) 
				{
					fillCount++;

					if (rectX1 > x)
						rectX1 = x;
					if (rectX2 < x)
						rectX2 = x;

					if (rectY1 > y)
						rectY1 = y;
					if (rectY2 < y)
						rectY2 = y;

					if (nearDepth > depth)
						nearDepth = depth;
					if (farDepth < depth)
						farDepth = depth;
				}

				index++;
			}

			rowIndex += depthWidth;
		}

		if (rectX1 < rectX2 && rectY1 < rectY2) 
		{
			rectResult.x = rectX1;
			rectResult.y = rectY1;
			rectResult.width = rectX2 - rectX1;
			rectResult.height = rectY2 - rectY1;

			depthMinMax.x = (float)nearDepth;  // min
			depthMinMax.y = (float)farDepth;   // max

			int totalCount = (maxX - minX) * (maxY - minY);
			fillRatio = totalCount > 0 ? (float)fillCount / (float)totalCount : 0f;
		}
	}

//	// tracks the depth in the given direction
//	private int TrackDepthInDirection(int index, int stepIndex, int minIndex, int maxIndex, byte userIndex, 
//									  ushort minDepth, ushort maxDepth, ref Vector2 depthMinMax)
//	{
//		int indexDiff = 0;
//		int validIndexDiff = 0;
//
//		ushort nearDepth = (ushort)depthMinMax.x;
//		ushort farDepth = (ushort)depthMinMax.y;
//
//		index += stepIndex;
//		while(index >= minIndex && index < maxIndex)
//		{
//			ushort depth = sensorData.depthImage[index];
//
//			if(sensorData.bodyIndexImage[index] == userIndex && depth != 0 &&
//				(depth >= minDepth && depth <= maxDepth))
//			{
//				validIndexDiff = indexDiff;
//
//				if (nearDepth > depth)
//					nearDepth = depth;
//				if (farDepth < depth)
//					farDepth = depth;
//			}
//
//			index += stepIndex;
//			indexDiff++;
//		}
//
//		depthMinMax.x = (float)nearDepth;  // min
//		depthMinMax.y = (float)farDepth;  // max
//
//		return validIndexDiff;
//	}

//	// calculates real object size from joint depth position and object rectangle
//	private bool CalculateObjectSize(Vector2 dposJoint, Rect drectObject, Vector2 depthMinMax, ref Vector3 sizeObject)
//	{
//		if (dposJoint == Vector2.zero || drectObject.width <= 1f || drectObject.height <= 1f)
//			return false;
//
//		// left
//		Vector2 dposLeft = new Vector2(drectObject.x, dposJoint.y);
//		ushort depthLeft = manager.GetDepthForPixel((int)dposLeft.x, (int)dposLeft.y);
//		Vector3 spaceLeft = manager.MapDepthPointToSpaceCoords(dposLeft, depthLeft, true);
//
//		// right
//		Vector2 dposRight = new Vector2(drectObject.x + drectObject.width - 1f, dposJoint.y);
//		ushort depthRight = manager.GetDepthForPixel((int)dposRight.x, (int)dposRight.y);
//		Vector3 spaceRight = manager.MapDepthPointToSpaceCoords(dposRight, depthRight, true);
//
//		// top
//		Vector2 dposTop = new Vector2(dposJoint.x, drectObject.y);
//		ushort depthTop = manager.GetDepthForPixel((int)dposTop.x, (int)dposTop.y);
//		Vector3 spaceTop = manager.MapDepthPointToSpaceCoords(dposTop, depthTop, true);
//
//		// bottom
//		Vector2 dposBottom = new Vector2(dposJoint.x, drectObject.y + drectObject.height - 1f);
//		ushort depthBottom = manager.GetDepthForPixel((int)dposBottom.x, (int)dposBottom.y);
//		Vector3 spaceBottom = manager.MapDepthPointToSpaceCoords(dposBottom, depthBottom, true);
//
//		// calculate size
//		sizeObject.x = spaceRight.x - spaceLeft.x;
//		sizeObject.y = spaceTop.y - spaceBottom.y;
//		sizeObject.z = depthMinMax.y > depthMinMax.x ? (depthMinMax.y - depthMinMax.x) / 1000f : 0f;
//
//		return true;
//	}

//	// calculates and draws the given depth rectangle on color camera texture
//	private void DrawColorRectAroundObject(Vector2 dposJoint, Rect drectObject)
//	{
//		if (manager && dposJoint != Vector2.zero && drectObject.width > 0 && drectObject.height > 0) 
//		{
//		}
//	}

}
