using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Light2D.Tests
{
    public class ContractTests
    {
        [Test]
        public void Point2_EqualityArithmeticAndConversionsAreStable()
        {
            var point = new Point2(3, -2);

            Assert.That(point, Is.EqualTo(new Point2(3, -2)));
            Assert.That(point.GetHashCode(), Is.EqualTo(new Point2(3, -2).GetHashCode()));
            Assert.That(point + new Point2(2, 5), Is.EqualTo(new Point2(5, 3)));
            Assert.That(point - new Point2(1, 1), Is.EqualTo(new Point2(2, -3)));
            Assert.That(point * 2, Is.EqualTo(new Point2(6, -4)));
            Assert.That(point / 2, Is.EqualTo(new Point2(1, -1)));
            Assert.That((Vector2)point, Is.EqualTo(new Vector2(3, -2)));
            Assert.That((Vector3)point, Is.EqualTo(new Vector3(3, -2, 0)));
            Assert.That(point.ToString(), Is.EqualTo("(3, -2)"));
        }

        [Test]
        public void Point2_FloorPreservesLegacyTruncationAndRoundUsesUnityRounding()
        {
            Assert.That(Point2.Floor(new Vector2(-1.9f, 2.9f)), Is.EqualTo(new Point2(-1, 2)));
            Assert.That(Point2.Round(-1.6f, 2.6f), Is.EqualTo(new Point2(-2, 3)));
            Assert.That(Point2.zero, Is.EqualTo(new Point2(0, 0)));
            Assert.That(Point2.one, Is.EqualTo(new Point2(1, 1)));
        }

        [Test]
        public void Util_VectorAngleAndCollectionContractsAreStable()
        {
            Assert.That(Util.ClampAngle(270), Is.EqualTo(-90).Within(0.0001f));
            Assert.That(Util.Cross(Vector2.right, Vector2.up), Is.EqualTo(1).Within(0.0001f));
            Assert.That(Vector2.right.Rotate90(), Is.EqualTo(Vector2.up));
            Assert.That(Vector2.up.AngleZ(), Is.EqualTo(0).Within(0.0001f));
            Assert.That(Vector2.right.AngleZ(), Is.EqualTo(-90).Within(0.0001f));
            Assert.That(new Vector2(4, 6).Div(new Vector2(2, 3)), Is.EqualTo(new Vector2(2, 2)));

            var values = new List<int> { 3, 1, 2 };
            Assert.That(values.IndexOfMin(value => value), Is.EqualTo(1));
            Assert.That(values.MinBy(value => value), Is.EqualTo(1));
            Assert.That(Array.Empty<int>().MinByOrDefault(value => value), Is.EqualTo(0));
            Assert.Throws<ArgumentException>(() => Array.Empty<int>().MinBy(value => value));
        }

        [Test]
        public void Util_SafeBackwardIterationAllowsRemoval()
        {
            var values = new List<int> { 0, 1, 2, 3 };

            values.SafeIterateBackward((value, index) =>
            {
                if ((value & 1) == 0)
                    values.RemoveAt(index);
            });

            Assert.That(values, Is.EqualTo(new[] { 1, 3 }));
        }
    }
}
