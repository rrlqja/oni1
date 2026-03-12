using Core.Simulation.Commands;
using Core.Simulation.Data;
using Core.Simulation.Definitions;
using Core.Simulation.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class SimulationRunnerSwapTests
    {
        private ElementRegistry CreateRegistry()
        {
            // 실제 프로젝트의 ElementDatabaseSO 생성 방식에 맞게 바꿔도 됨.
            // 여기선 개념 예시라면 기존 테스트 유틸/더미 팩토리 사용 권장.
            Assert.Fail("프로젝트의 실제 ElementDatabaseSO 생성 유틸에 맞춰 registry 생성 헬퍼를 연결해야 합니다.");
            return null;
        }

        [Test]
        public void FallingSolid_Swaps_With_Gas_Below()
        {
            // 이 테스트는 구조 예시다.
            // 실제 프로젝트의 테스트 헬퍼가 있으면 그쪽으로 맞춰 연결하면 된다.

            // Arrange
            var registry = CreateRegistry();
            var grid = new WorldGrid(5, 5);
            var runner = new SimulationRunner(grid, registry);

            // Sand at (2, 3), Gas at (2, 2)
            // Tick once

            // Assert
            // Sand should be at (2, 2)
            // Gas should be at (2, 3)
        }
    }
}